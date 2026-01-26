using System;
using System.Collections.Generic;
using System.Numerics;

namespace physics.Engine.Helpers
{
    /// <summary>
    /// Provides algorithms for decomposing concave polygons into convex pieces.
    /// Uses ear clipping to triangulate, then merges triangles into larger convex polygons.
    /// </summary>
    public static class PolygonDecomposition
    {
        /// <summary>
        /// Decomposes a potentially concave polygon into convex sub-polygons.
        /// Uses ear clipping triangulation followed by greedy merging of adjacent triangles.
        /// </summary>
        /// <param name="vertices">Input polygon vertices (can be concave). Must be in consistent winding order.</param>
        /// <returns>List of convex polygons, each represented as an array of vertices with negative signed area (matching BoxPhysShape convention).</returns>
        public static List<Vector2[]> DecomposeToConvex(Vector2[] vertices)
        {
            if (vertices.Length < 3)
                return new List<Vector2[]>();

            // If already convex, normalize winding to match physics system convention
            if (IsConvex(vertices))
            {
                var normalized = new List<Vector2>(vertices);
                // Physics system (BoxPhysShape) uses NEGATIVE signed area (CCW in screen coords)
                // This is important for SutherlandHodgmanClip which uses cross product to determine "inside"
                if (GetSignedArea(normalized) > 0)
                    normalized.Reverse();
                return new List<Vector2[]> { normalized.ToArray() };
            }

            // Step 1: Triangulate using ear clipping
            var triangles = EarClipTriangulate(vertices);

            // Step 2: Merge triangles into larger convex polygons
            var convexPolygons = MergeTrianglesGreedy(triangles);

            return convexPolygons;
        }

        /// <summary>
        /// Triangulates a simple polygon using the ear clipping algorithm.
        /// </summary>
        /// <param name="vertices">Polygon vertices in consistent winding order.</param>
        /// <returns>List of triangles, each as a 3-element Vector2 array.</returns>
        public static List<Vector2[]> EarClipTriangulate(Vector2[] vertices)
        {
            var triangles = new List<Vector2[]>();

            if (vertices.Length < 3)
                return triangles;

            // Work with a mutable list
            var remaining = new List<Vector2>(vertices);

            // Ensure consistent winding: normalize to CW (positive area in Y-down coords)
            if (GetSignedArea(remaining) < 0)
                remaining.Reverse();

            while (remaining.Count > 3)
            {
                bool earFound = false;

                for (int i = 0; i < remaining.Count; i++)
                {
                    int prev = (i - 1 + remaining.Count) % remaining.Count;
                    int next = (i + 1) % remaining.Count;

                    Vector2 a = remaining[prev];
                    Vector2 b = remaining[i];
                    Vector2 c = remaining[next];

                    // Check if this is a convex vertex (ear candidate)
                    if (!IsConvexVertex(a, b, c))
                        continue;

                    // Check if any other vertex is inside this triangle
                    bool containsOther = false;
                    for (int j = 0; j < remaining.Count; j++)
                    {
                        if (j == prev || j == i || j == next)
                            continue;

                        if (PointInTriangle(remaining[j], a, b, c))
                        {
                            containsOther = true;
                            break;
                        }
                    }

                    if (!containsOther)
                    {
                        // Found an ear - clip it
                        triangles.Add(new Vector2[] { a, b, c });
                        remaining.RemoveAt(i);
                        earFound = true;
                        break;
                    }
                }

                if (!earFound)
                {
                    // Degenerate case - shouldn't happen with valid simple polygons
                    break;
                }
            }

            // Add the final triangle
            if (remaining.Count == 3)
            {
                triangles.Add(remaining.ToArray());
            }

            return triangles;
        }

        /// <summary>
        /// Merges adjacent triangles into larger convex polygons using a greedy approach.
        /// </summary>
        private static List<Vector2[]> MergeTrianglesGreedy(List<Vector2[]> triangles)
        {
            if (triangles.Count == 0)
                return new List<Vector2[]>();

            var polygons = new List<List<Vector2>>();
            foreach (var tri in triangles)
            {
                polygons.Add(new List<Vector2>(tri));
            }

            bool merged;
            do
            {
                merged = false;
                for (int i = 0; i < polygons.Count && !merged; i++)
                {
                    for (int j = i + 1; j < polygons.Count && !merged; j++)
                    {
                        var mergedPoly = TryMergePolygons(polygons[i], polygons[j]);
                        if (mergedPoly != null && IsConvex(mergedPoly.ToArray()))
                        {
                            polygons[i] = mergedPoly;
                            polygons.RemoveAt(j);
                            merged = true;
                        }
                    }
                }
            } while (merged);

            var result = new List<Vector2[]>();
            foreach (var poly in polygons)
            {
                // EarClipTriangulate normalizes to positive signed area internally,
                // but physics system (BoxPhysShape) uses NEGATIVE signed area
                // So we reverse each polygon before returning
                poly.Reverse();
                result.Add(poly.ToArray());
            }
            return result;
        }

        /// <summary>
        /// Attempts to merge two polygons that share an edge.
        /// Returns the merged polygon if successful, null otherwise.
        /// </summary>
        private static List<Vector2>? TryMergePolygons(List<Vector2> polyA, List<Vector2> polyB)
        {
            const float epsilon = 0.01f;
            float epsilonSq = epsilon * epsilon;

            // Find shared edge between polyA and polyB
            for (int i = 0; i < polyA.Count; i++)
            {
                int nextI = (i + 1) % polyA.Count;
                Vector2 a1 = polyA[i];      // Start of edge in A
                Vector2 a2 = polyA[nextI];  // End of edge in A

                for (int j = 0; j < polyB.Count; j++)
                {
                    int nextJ = (j + 1) % polyB.Count;
                    Vector2 b1 = polyB[j];      // Start of edge in B
                    Vector2 b2 = polyB[nextJ];  // End of edge in B

                    // Check if edges match (opposite direction: A's a1->a2 matches B's b2<-b1)
                    // This means a1 ≈ b2 and a2 ≈ b1
                    if (Vector2.DistanceSquared(a1, b2) < epsilonSq &&
                        Vector2.DistanceSquared(a2, b1) < epsilonSq)
                    {
                        // Build merged polygon by walking both polygons, inserting B's vertices 
                        // into A at the shared edge location.
                        //
                        // polyA has edge from index i to nextI
                        // polyB has edge from index j to nextJ (which goes the opposite direction)
                        //
                        // Merged polygon: 
                        // - Start with vertices of A from nextI, going around to i (exclusive) 
                        // - Then insert vertices of B from nextJ+1 going around to j (exclusive)
                        // - This replaces the shared edge with B's "other side"

                        var merged = new List<Vector2>();

                        // Add vertices from polyA starting at nextI (a2), going around, 
                        // include a1 (i), which is where we'll splice in B
                        for (int k = 0; k < polyA.Count; k++)
                        {
                            int idx = (nextI + k) % polyA.Count;
                            if (idx == nextI && k > 0)
                                break; // Back to start, done with A

                            merged.Add(polyA[idx]);

                            // After adding a1 (when idx == i), splice in B's vertices
                            if (idx == i)
                            {
                                // Insert B's vertices: from nextJ+1, around to j (exclusive)
                                // These are all vertices of B except b1 (j) and b2 (nextJ)
                                for (int m = 1; m < polyB.Count - 1; m++)
                                {
                                    int bIdx = (nextJ + 1 + m - 1) % polyB.Count;
                                    // Skip if we've wrapped around to j or nextJ
                                    if (bIdx == j || bIdx == nextJ)
                                        continue;
                                    merged.Add(polyB[bIdx]);
                                }
                            }
                        }

                        // Clean up collinear vertices
                        merged = RemoveCollinearVertices(merged);

                        if (merged.Count >= 3)
                            return merged;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Removes collinear vertices from a polygon.
        /// </summary>
        private static List<Vector2> RemoveCollinearVertices(List<Vector2> vertices)
        {
            const float epsilon = 0.0001f;
            var result = new List<Vector2>();

            for (int i = 0; i < vertices.Count; i++)
            {
                int prev = (i - 1 + vertices.Count) % vertices.Count;
                int next = (i + 1) % vertices.Count;

                Vector2 a = vertices[prev];
                Vector2 b = vertices[i];
                Vector2 c = vertices[next];

                // Check if b is collinear with a and c
                float cross = Cross(b - a, c - a);
                if (MathF.Abs(cross) > epsilon)
                {
                    result.Add(b);
                }
            }

            return result.Count >= 3 ? result : vertices;
        }


        /// <summary>
        /// Tests if a polygon is convex.
        /// </summary>
        public static bool IsConvex(Vector2[] vertices)
        {
            if (vertices.Length < 3)
                return false;

            int n = vertices.Length;
            int sign = 0;

            for (int i = 0; i < n; i++)
            {
                Vector2 a = vertices[i];
                Vector2 b = vertices[(i + 1) % n];
                Vector2 c = vertices[(i + 2) % n];

                float cross = Cross(b - a, c - b);

                if (MathF.Abs(cross) > 0.0001f)
                {
                    int currentSign = cross > 0 ? 1 : -1;
                    if (sign == 0)
                        sign = currentSign;
                    else if (sign != currentSign)
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Returns the signed area of a polygon.
        /// In Y-down screen coordinates: Positive = CW, Negative = CCW.
        /// </summary>
        private static float GetSignedArea(List<Vector2> vertices)
        {
            float area = 0;
            for (int i = 0; i < vertices.Count; i++)
            {
                int j = (i + 1) % vertices.Count;
                area += vertices[i].X * vertices[j].Y;
                area -= vertices[j].X * vertices[i].Y;
            }
            return area / 2f;
        }

        /// <summary>
        /// Tests if vertex B is a convex vertex in the sequence A-B-C.
        /// </summary>
        private static bool IsConvexVertex(Vector2 a, Vector2 b, Vector2 c)
        {
            return Cross(b - a, c - b) > 0;
        }

        /// <summary>
        /// Tests if point P is inside triangle ABC.
        /// </summary>
        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Sign(p, a, b);
            float d2 = Sign(p, b, c);
            float d3 = Sign(p, c, a);

            bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
            bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);

            return !(hasNeg && hasPos);
        }

        private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
        {
            return (p1.X - p3.X) * (p2.Y - p3.Y) - (p2.X - p3.X) * (p1.Y - p3.Y);
        }

        /// <summary>
        /// 2D cross product (returns scalar).
        /// </summary>
        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.X * b.Y - a.Y * b.X;
        }

        /// <summary>
        /// Computes the centroid of a polygon.
        /// </summary>
        public static Vector2 ComputeCentroid(Vector2[] vertices)
        {
            float cx = 0, cy = 0;
            float signedArea = 0;

            for (int i = 0; i < vertices.Length; i++)
            {
                int j = (i + 1) % vertices.Length;
                float cross = vertices[i].X * vertices[j].Y - vertices[j].X * vertices[i].Y;
                signedArea += cross;
                cx += (vertices[i].X + vertices[j].X) * cross;
                cy += (vertices[i].Y + vertices[j].Y) * cross;
            }

            signedArea *= 0.5f;
            if (MathF.Abs(signedArea) < 0.0001f)
            {
                // Fallback for degenerate polygons
                Vector2 sum = Vector2.Zero;
                foreach (var v in vertices)
                    sum += v;
                return sum / vertices.Length;
            }

            cx /= (6f * signedArea);
            cy /= (6f * signedArea);

            return new Vector2(cx, cy);
        }
    }
}
