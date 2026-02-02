using System;
using System.Collections.Generic;
using SharpPhysics.Engine.Classes;
using SharpPhysics.Engine.Objects;
using SharpPhysics.Engine.Shapes;
using System.Linq;
using System.Numerics;

public static class CollisionHelpers
{
    // Computes the four corners of a rectangle (OBB) in world space.
    public static List<Vector2> GetRectangleCorners(PhysicsObject obj)
    {
        var box = (BoxPhysShape)obj.Shape;

        List<Vector2> corners = new List<Vector2>(4);
        float halfW = box.Width / 2f;
        float halfH = box.Height / 2f;

        // Define corners in local space in clockwise order.
        // The Sutherland-Hodgman algorithm expects the clip polygon to be in counterclockwise order, however, we're in
        // a coordinate system where the y-axis points down, so we define the corners in clockwise order.
        // For example, starting at the bottom-right:
        // bottom-right, bottom-left, top-left, top-right.
        Vector2[] localCorners = new Vector2[]
        {
        new Vector2( halfW, -halfH),   // bottom-right
        new Vector2(-halfW, -halfH),   // bottom-left
        new Vector2(-halfW,  halfH),   // top-left
        new Vector2( halfW,  halfH)    // top-right
        };

        float cos = (float)Math.Cos(obj.Angle);
        float sin = (float)Math.Sin(obj.Angle);
        foreach (var lc in localCorners)
        {
            // Rotate the local corner and translate to world space.
            float worldX = obj.Center.X + lc.X * cos - lc.Y * sin;
            float worldY = obj.Center.Y + lc.X * sin + lc.Y * cos;
            corners.Add(new Vector2(worldX, worldY));
        }
        return corners;
    }



    public static List<Vector2> SutherlandHodgmanClip(Vector2[] subjectPolygon, Vector2[] clipPolygon)
    {
        // Start with the subject polygon.
        List<Vector2> poly = new (subjectPolygon);
        // For each edge of the clip polygon:
        int clipCount = clipPolygon.Length;
        for (int i = 0; i < clipCount; i++)
        {
            int next = (i + 1) % clipCount;
            Vector2 clipEdgeStart = clipPolygon[i];
            Vector2 clipEdgeEnd = clipPolygon[next];
            poly = ClipEdge(poly, clipEdgeStart, clipEdgeEnd);
            if (poly.Count == 0)
                return subjectPolygon.ToList();
        }

        return poly;
    }

    /// <summary>
    /// Clips a polygon (poly) against a single clip edge defined by clipEdgeStart and clipEdgeEnd.
    /// This function implements the logic from the provided C++ code, using floats.
    /// </summary>
    private static List<Vector2> ClipEdge(List<Vector2> poly, Vector2 clipEdgeStart, Vector2 clipEdgeEnd)
    {
        List<Vector2> newPoly = new List<Vector2>();
        int polySize = poly.Count;
        if (polySize == 0)
            return newPoly;

        // Iterate over each edge of the polygon.
        for (int i = 0; i < polySize; i++)
        {
            int k = (i + 1) % polySize;
            Vector2 current = poly[i];
            Vector2 next = poly[k];

            // Compute the "position" of the points relative to the clip edge.
            // A point is considered "inside" if this value is < 0.
            float currentPos = (clipEdgeEnd.X - clipEdgeStart.X) * (current.Y - clipEdgeStart.Y)
                               - (clipEdgeEnd.Y - clipEdgeStart.Y) * (current.X - clipEdgeStart.X);
            float nextPos = (clipEdgeEnd.X - clipEdgeStart.X) * (next.Y - clipEdgeStart.Y)
                            - (clipEdgeEnd.Y - clipEdgeStart.Y) * (next.X - clipEdgeStart.X);

            // Case 1: Both points are inside.
            if (currentPos < 0 && nextPos < 0)
            {
                // Add the second point.
                newPoly.Add(next);
            }
            // Case 2: Current is outside, next is inside.
            else if (currentPos >= 0 && nextPos < 0)
            {
                // Add the intersection point and the next point.
                Vector2 intersect = ComputeIntersection(current, next, clipEdgeStart, clipEdgeEnd);
                newPoly.Add(intersect);
                newPoly.Add(next);
            }
            // Case 3: Current is inside, next is outside.
            else if (currentPos < 0 && nextPos >= 0)
            {
                // Add the intersection point only.
                Vector2 intersect = ComputeIntersection(current, next, clipEdgeStart, clipEdgeEnd);
                newPoly.Add(intersect);
            }
            // Case 4: Both are outside – add nothing.
        }
        return newPoly;
    }

    /// <summary>
    /// Computes the intersection point of the infinite lines through points s->e and cp1->cp2.
    /// This is the same as our existing ComputeIntersection, but included here for clarity.
    /// </summary>
    public static Vector2 ComputeIntersection(Vector2 s, Vector2 e, Vector2 cp1, Vector2 cp2)
    {
        Vector2 dc = cp1 - cp2;
        Vector2 dp = s - e;
        float n1 = cp1.X * cp2.Y - cp1.Y * cp2.X;
        float n2 = s.X * e.Y - s.Y * e.X;
        float denom = dc.X * dp.Y - dc.Y * dp.X;
        if (Math.Abs(denom) < 1e-6f)
            return s; // Lines are parallel; return s as fallback.
        float x = (n1 * dp.X - n2 * dc.X) / denom;
        float y = (n1 * dp.Y - n2 * dc.Y) / denom;
        return new Vector2(x, y);
    }


        // Computes the centroid (center of mass) of a polygon.
    public static Vector2 ComputeCentroid(List<Vector2> polygon)
    {
        float accumulatedArea = 0f;
        float centerX = 0f;
        float centerY = 0f;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i, i++)
        {
            float temp = polygon[j].X * polygon[i].Y - polygon[i].X * polygon[j].Y;
            accumulatedArea += temp;
            centerX += (polygon[j].X + polygon[i].X) * temp;
            centerY += (polygon[j].Y + polygon[i].Y) * temp;
        }

        // Much greater than the recommended epsilon of 1e-6, but the system becomes unstable below this
        if (Math.Abs(accumulatedArea) < 0.5f)
            return polygon[0];

        // Multiply accumulatedArea by 3 to get the proper divisor (6 * area).
        accumulatedArea *= 3f;
        return new Vector2(centerX / accumulatedArea, centerY / accumulatedArea);
    }

    // This function computes the actual contact point between two rotated rectangles.
    // It clips rectangle A's corners against rectangle B and computes the centroid of the intersection polygon.
    public static void UpdateContactPoint(ref Manifold m)
    {
        Vector2[] polyA = m.A.Shape.GetTransformedVertices(m.A.Center, m.A.Angle);
        Vector2[] polyB = m.B.Shape.GetTransformedVertices(m.B.Center, m.B.Angle);

        List<Vector2> intersection = SutherlandHodgmanClip(polyA, polyB);
        if (intersection.Count == 0)
        {
            // Fallback: use midpoint between centers.
            m.ContactPoint = (m.A.Center + m.B.Center) * 0.5f;
        }
        else
        {
            m.ContactPoint = ComputeCentroid(intersection);
        }
    }

    // Helper: 2D cross product returning a scalar.
    public static float Cross(Vector2 a, Vector2 b)
    {
        return a.X * b.Y - a.Y * b.X;
    }
}
