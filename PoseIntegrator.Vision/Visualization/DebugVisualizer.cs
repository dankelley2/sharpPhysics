using System.Numerics;
using OpenCvSharp;
using PoseIntegrator.Vision.Models;

namespace PoseIntegrator.Vision.Visualization;

/// <summary>
/// Debug visualizer for person detection - shows camera feed, mask overlay, and polygon points.
/// </summary>
public sealed class DebugVisualizer : IDisposable
{
    private readonly string _windowName;
    private bool _isWindowCreated;
    private readonly object _syncLock = new();
    
    public DebugVisualizer(string windowName = "Person Detection Debug")
    {
        _windowName = windowName;
    }

    /// <summary>
    /// Visualizes the detection result on the input frame.
    /// </summary>
    /// <param name="frame">Input camera frame.</param>
    /// <param name="segResult">Segmentation result with mask.</param>
    /// <param name="physicsResult">Physics polygons result.</param>
    public void Visualize(Mat frame, SegmentationResult segResult, PhysicsPolygonsResult physicsResult)
    {
        lock (_syncLock)
        {
            if (frame.Empty())
                return;

            // Create window if needed
            if (!_isWindowCreated)
            {
                Cv2.NamedWindow(_windowName, WindowFlags.AutoSize);
                _isWindowCreated = true;
            }

            // Clone frame to avoid modifying original
            using var display = frame.Clone();

            // Overlay mask as semi-transparent green
            if (segResult.Mask != null && segResult.Mask.Length > 0)
            {
                OverlayMask(display, segResult.Mask, segResult.Width, segResult.Height);
            }

            // Draw polygons
            if (physicsResult.ConvexPolygons.Count > 0)
            {
                DrawPolygons(display, physicsResult.ConvexPolygons);
            }

            // Draw bounding box
            if (physicsResult.BoundingBoxPixels.Width > 0 && physicsResult.BoundingBoxPixels.Height > 0)
            {
                var bbox = physicsResult.BoundingBoxPixels;
                Cv2.Rectangle(display, 
                    new Point(bbox.X, bbox.Y), 
                    new Point(bbox.Right, bbox.Bottom),
                    new Scalar(255, 255, 0), 2); // Yellow bounding box
            }

            // Draw stats
            DrawStats(display, physicsResult);

            // Display
            Cv2.ImShow(_windowName, display);
            Cv2.WaitKey(1); // Process window events
        }
    }

    private static unsafe void OverlayMask(Mat display, byte[] mask, int maskWidth, int maskHeight)
    {
        // Create binary mask from byte array
        using var binaryMask = new Mat(maskHeight, maskWidth, MatType.CV_8UC1);

        var ptr = (byte*)binaryMask.DataPointer;
        for (int i = 0; i < mask.Length; i++)
        {
            ptr[i] = mask[i];
        }

        // Resize mask to frame dimensions
        using var resizedMask = new Mat();
        Cv2.Resize(binaryMask, resizedMask, display.Size());

        // Create green overlay
        using var greenOverlay = new Mat(display.Size(), MatType.CV_8UC3, new Scalar(0, 255, 0));

        // Blend with alpha
        using var maskedGreen = new Mat();
        greenOverlay.CopyTo(maskedGreen, resizedMask);

        Cv2.AddWeighted(display, 0.7, maskedGreen, 0.3, 0, display);
    }

    private static void DrawPolygons(Mat display, IReadOnlyList<IReadOnlyList<Vector2>> polygons)
    {
        int frameWidth = display.Width;
        int frameHeight = display.Height;

        foreach (var polygon in polygons)
        {
            if (polygon.Count < 2)
                continue;

            // Convert normalized coordinates to pixel coordinates
            var points = polygon.Select(p => new Point(
                (int)(p.X * frameWidth),
                (int)(p.Y * frameHeight)
            )).ToArray();

            // Draw polygon outline
            for (int i = 0; i < points.Length; i++)
            {
                var p1 = points[i];
                var p2 = points[(i + 1) % points.Length];
                Cv2.Line(display, p1, p2, new Scalar(0, 255, 255), 2); // Cyan outline
            }

            // Draw vertex points
            foreach (var pt in points)
            {
                Cv2.Circle(display, pt, 3, new Scalar(255, 0, 255), -1); // Magenta points
            }
        }
    }

    private static void DrawStats(Mat display, PhysicsPolygonsResult result)
    {
        int y = 30;
        var font = HersheyFonts.HersheySimplex;
        var fontScale = 0.6;
        var thickness = 2;
        var textColor = new Scalar(255, 255, 255);
        var bgColor = new Scalar(0, 0, 0);

        void DrawText(string text)
        {
            // Draw text background
            var textSize = Cv2.GetTextSize(text, font, fontScale, thickness, out var baseline);
            Cv2.Rectangle(display, 
                new Point(5, y - textSize.Height - 5), 
                new Point(15 + textSize.Width, y + 5),
                bgColor, -1);
            
            // Draw text
            Cv2.PutText(display, text, new Point(10, y), font, fontScale, textColor, thickness);
            y += 30;
        }

        DrawText($"Confidence: {result.Confidence:F2}");
        DrawText($"Polygons: {result.ConvexPolygons.Count}");
        
        if (result.ConvexPolygons.Count > 0)
        {
            int totalVertices = result.ConvexPolygons.Sum(p => p.Count);
            DrawText($"Vertices: {totalVertices}");
        }

        if (result.BoundingBoxPixels.Width > 0)
        {
            DrawText($"BBox: {result.BoundingBoxPixels.Width}x{result.BoundingBoxPixels.Height}");
        }
    }

    public void Dispose()
    {
        lock (_syncLock)
        {
            if (_isWindowCreated)
            {
                try
                {
                    Cv2.DestroyWindow(_windowName);
                }
                catch
                {
                    // Ignore errors during cleanup
                }
                _isWindowCreated = false;
            }
        }
    }
}
