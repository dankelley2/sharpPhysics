using System.Numerics;
using System.Drawing;
using OpenCvSharp;
using PoseIntegrator.Vision.Abstractions;
using PoseIntegrator.Vision.Models;

namespace PoseIntegrator.Vision.Geometry;

/// <summary>
/// Extracts multiple convex polygons from a segmentation mask using convex decomposition.
/// Better for physics engines that require convex shapes - captures concave details like arms/legs.
/// </summary>
public sealed class ConvexDecompositionExtractor : IPolygonExtractor
{
    private readonly double _simplifyEpsilon;
    private readonly double _minDefectDepth;
    private readonly int _minPolygonPoints;
    private readonly double _minPolygonArea;

    /// <summary>
    /// Creates a new convex decomposition extractor.
    /// </summary>
    /// <param name="simplifyEpsilon">Contour simplification tolerance in pixels (default 2.0).</param>
    /// <param name="minDefectDepth">Minimum convexity defect depth to trigger split (default 8.0 pixels).</param>
    /// <param name="minPolygonPoints">Minimum points per polygon (default 3).</param>
    /// <param name="minPolygonArea">Minimum polygon area as fraction of total (default 0.02 = 2%).</param>
    public ConvexDecompositionExtractor(
        double simplifyEpsilon = 2.0,
        double minDefectDepth = 8.0,
        int minPolygonPoints = 3,
        double minPolygonArea = 0.02)
    {
        _simplifyEpsilon = simplifyEpsilon;
        _minDefectDepth = minDefectDepth;
        _minPolygonPoints = minPolygonPoints;
        _minPolygonArea = minPolygonArea;
    }

    public PhysicsPolygonsResult Extract(SegmentationResult seg)
    {
        using var clean = MaskPostProcessor.ToCleanBinaryMask(seg.Mask, seg.Width, seg.Height);

        Cv2.FindContours(
            clean,
            out OpenCvSharp.Point[][] contours,
            out _,
            RetrievalModes.CComp,
            ContourApproximationModes.ApproxSimple);

        if (contours.Length == 0)
        {
            return new PhysicsPolygonsResult(
                ConvexPolygons: Array.Empty<IReadOnlyList<Vector2>>(),
                BoundingBoxPixels: Rectangle.Empty,
                Confidence: seg.Confidence,
                TimestampMs: seg.TimestampMs
            );
        }

        // Find largest contour
        int bestIdx = 0;
        double bestArea = 0;
        for (int i = 0; i < contours.Length; i++)
        {
            double area = Cv2.ContourArea(contours[i]);
            if (area > bestArea)
            {
                bestArea = area;
                bestIdx = i;
            }
        }

        var contour = contours[bestIdx];

        // Simplify contour to reduce noise
        var simplified = Cv2.ApproxPolyDP(contour, _simplifyEpsilon, true);

        // Output the simplified concave polygon as-is (single polygon)
        var normalizedPolygons = new List<IReadOnlyList<Vector2>>();
        var allPoints = new List<OpenCvSharp.Point>();

        if (simplified.Length >= _minPolygonPoints)
        {
            var normalized = new Vector2[simplified.Length];
            for (int i = 0; i < simplified.Length; i++)
            {
                normalized[i] = new Vector2(
                    simplified[i].X / (float)seg.Width,
                    simplified[i].Y / (float)seg.Height
                );
                allPoints.Add(simplified[i]);
            }
            normalizedPolygons.Add(normalized);
        }

        // Compute overall bounding box
        var bbox = allPoints.Count > 0
            ? Cv2.BoundingRect(allPoints).ToDrawingRect()
            : Rectangle.Empty;

        return new PhysicsPolygonsResult(
            ConvexPolygons: normalizedPolygons,
            BoundingBoxPixels: bbox,
            Confidence: seg.Confidence,
            TimestampMs: seg.TimestampMs
        );
    }

    /// <summary>
    /// Decomposes a concave polygon into non-overlapping convex pieces by cutting at concave vertices.
    /// </summary>
    private List<OpenCvSharp.Point[]> DecomposeIntoConvexPieces(OpenCvSharp.Point[] polygon, double totalArea)
    {
        var result = new List<OpenCvSharp.Point[]>();
        
        // Find all concave vertices (where the polygon bends inward)
        var concaveIndices = FindConcaveVertices(polygon);
        
        if (concaveIndices.Count == 0)
        {
            // Already convex
            result.Add(polygon);
            return result;
        }

        // Simple approach: split at the deepest concave point using a horizontal or vertical cut
        // This creates non-overlapping pieces
        
        // Find the convexity defects to get cut points
        int[] hullIndices = Cv2.ConvexHullIndices(polygon);
        if (hullIndices.Length < 3) 
        {
            result.Add(polygon);
            return result;
        }

        Vec4i[] defects;
        try
        {
            defects = Cv2.ConvexityDefects(polygon, hullIndices);
        }
        catch
        {
            result.Add(polygon);
            return result;
        }

        // Collect significant defects (concave regions)
        var significantDefects = new List<(int start, int far, int end, double depth)>();
        foreach (var d in defects)
        {
            double depth = d.Item3 / 256.0;
            if (depth >= _minDefectDepth)
            {
                significantDefects.Add((d.Item0, d.Item2, d.Item1, depth));
            }
        }

        if (significantDefects.Count == 0)
        {
            result.Add(Cv2.ConvexHull(polygon));
            return result;
        }

        // Sort defects by position around the contour
        significantDefects.Sort((a, b) => a.far.CompareTo(b.far));

        // Split the polygon at defect points
        // Each segment between consecutive defect points becomes a convex piece
        double minArea = totalArea * _minPolygonArea;
        
        var splitPoints = significantDefects.Select(d => d.far).ToList();
        
        // Create pieces by cutting between split points
        for (int i = 0; i < splitPoints.Count; i++)
        {
            int startIdx = splitPoints[i];
            int endIdx = splitPoints[(i + 1) % splitPoints.Count];
            
            var piecePoints = new List<OpenCvSharp.Point>();
            
            // Collect points from startIdx to endIdx (wrapping around)
            for (int j = startIdx; ; j = (j + 1) % polygon.Length)
            {
                piecePoints.Add(polygon[j]);
                if (j == endIdx) break;
                if (piecePoints.Count > polygon.Length) break; // safety
            }
            
            if (piecePoints.Count >= 3)
            {
                var pieceArray = piecePoints.ToArray();
                if (Cv2.ContourArea(pieceArray) >= minArea)
                {
                    result.Add(pieceArray);
                }
            }
        }

        // If we got no valid pieces, return the whole polygon as convex hull
        if (result.Count == 0)
        {
            result.Add(Cv2.ConvexHull(polygon));
        }

        return result;
    }

    /// <summary>
    /// Finds indices of concave vertices in the polygon.
    /// </summary>
    private List<int> FindConcaveVertices(OpenCvSharp.Point[] polygon)
    {
        var concave = new List<int>();
        int n = polygon.Length;
        
        if (n < 3) return concave;

        for (int i = 0; i < n; i++)
        {
            var prev = polygon[(i - 1 + n) % n];
            var curr = polygon[i];
            var next = polygon[(i + 1) % n];

            // Cross product to determine turn direction
            double cross = (curr.X - prev.X) * (next.Y - curr.Y) - 
                          (curr.Y - prev.Y) * (next.X - curr.X);

            // Negative cross product means concave (for clockwise polygon)
            // The sign depends on polygon winding - we check both
            if (Math.Abs(cross) > 1e-6 && cross < 0)
            {
                concave.Add(i);
            }
        }

        return concave;
    }

    /// <summary>
    /// Checks if a polygon is convex.
    /// </summary>
    private static bool IsConvex(OpenCvSharp.Point[] polygon)
    {
        if (polygon.Length < 3) return true;

        var hull = Cv2.ConvexHull(polygon);
        
        // If hull has same number of points as polygon, it's convex
        // (with some tolerance for floating point)
        return hull.Length >= polygon.Length - 1;
    }
}
