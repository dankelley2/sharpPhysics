using System;
using System.Collections.Generic;
using SFML.System;
using physics.Engine.Classes;
using physics.Engine.Shaders;
using physics.Engine.Objects;
using physics.Engine.Shapes;

public static class CollisionHelpers
{
    // Computes the four corners of a rectangle (OBB) in world space.
    public static List<Vector2f> GetRectangleCorners(PhysicsObject obj)
    {
        if (!(obj.Shape is BoxPhysShape box))
        {
            throw new ArgumentException("GetRectangleCorners requires a PhysicsObject with a BoxPhysShape.");
        }

        List<Vector2f> corners = new List<Vector2f>(4);
        float halfW = box.Width / 2f;
        float halfH = box.Height / 2f;

        // Define corners in local space in clockwise order.
        // The Sutherland-Hodgman algorithm expects the clip polygon to be in counterclockwise order, however, we're in
        // a coordinate system where the y-axis points down, so we define the corners in clockwise order.
        // For example, starting at the bottom-right:
        // bottom-right, bottom-left, top-left, top-right.
        Vector2f[] localCorners = new Vector2f[]
        {
        new Vector2f( halfW, -halfH),   // bottom-right
        new Vector2f(-halfW, -halfH),   // bottom-left
        new Vector2f(-halfW,  halfH),   // top-left
        new Vector2f( halfW,  halfH)    // top-right
        };

        float cos = (float)Math.Cos(obj.Angle);
        float sin = (float)Math.Sin(obj.Angle);
        foreach (var lc in localCorners)
        {
            // Rotate the local corner and translate to world space.
            float worldX = obj.Center.X + lc.X * cos - lc.Y * sin;
            float worldY = obj.Center.Y + lc.X * sin + lc.Y * cos;
            corners.Add(new Vector2f(worldX, worldY));
        }
        return corners;
    }



    public static List<Vector2f> SutherlandHodgmanClip(List<Vector2f> subjectPolygon, List<Vector2f> clipPolygon)
    {
        // Start with the subject polygon.
        List<Vector2f> poly = new List<Vector2f>(subjectPolygon);
        // For each edge of the clip polygon:
        int clipCount = clipPolygon.Count;
        for (int i = 0; i < clipCount; i++)
        {
            int next = (i + 1) % clipCount;
            Vector2f clipEdgeStart = clipPolygon[i];
            Vector2f clipEdgeEnd = clipPolygon[next];
            poly = ClipEdge(poly, clipEdgeStart, clipEdgeEnd);
            if (poly.Count == 0)
                return subjectPolygon;
        }

        return poly;
    }

    /// <summary>
    /// Clips a polygon (poly) against a single clip edge defined by clipEdgeStart and clipEdgeEnd.
    /// This function implements the logic from the provided C++ code, using floats.
    /// </summary>
    private static List<Vector2f> ClipEdge(List<Vector2f> poly, Vector2f clipEdgeStart, Vector2f clipEdgeEnd)
    {
        List<Vector2f> newPoly = new List<Vector2f>();
        int polySize = poly.Count;
        if (polySize == 0)
            return newPoly;

        // Iterate over each edge of the polygon.
        for (int i = 0; i < polySize; i++)
        {
            int k = (i + 1) % polySize;
            Vector2f current = poly[i];
            Vector2f next = poly[k];

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
                Vector2f intersect = ComputeIntersection(current, next, clipEdgeStart, clipEdgeEnd);
                newPoly.Add(intersect);
                newPoly.Add(next);
            }
            // Case 3: Current is inside, next is outside.
            else if (currentPos < 0 && nextPos >= 0)
            {
                // Add the intersection point only.
                Vector2f intersect = ComputeIntersection(current, next, clipEdgeStart, clipEdgeEnd);
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
    public static Vector2f ComputeIntersection(Vector2f s, Vector2f e, Vector2f cp1, Vector2f cp2)
    {
        Vector2f dc = cp1 - cp2;
        Vector2f dp = s - e;
        float n1 = cp1.X * cp2.Y - cp1.Y * cp2.X;
        float n2 = s.X * e.Y - s.Y * e.X;
        float denom = dc.X * dp.Y - dc.Y * dp.X;
        if (Math.Abs(denom) < 1e-6f)
            return s; // Lines are parallel; return s as fallback.
        float x = (n1 * dp.X - n2 * dc.X) / denom;
        float y = (n1 * dp.Y - n2 * dc.Y) / denom;
        return new Vector2f(x, y);
    }


    // Helper: Computes the signed area of a polygon.
    // Positive area means vertices are in counter-clockwise order.
    public static float ComputeSignedArea(List<Vector2f> poly)
    {
        float area = 0f;
        for (int i = 0; i < poly.Count; i++)
        {
            int j = (i + 1) % poly.Count;
            area += (poly[i].X * poly[j].Y) - (poly[j].X * poly[i].Y);
        }
        return area / 2f;
    }


    // Returns true if point p is inside the half-space defined by edge from a to b.
    // Assumes clip polygon is defined in counterclockwise order.
    public static bool IsInside(Vector2f a, Vector2f b, Vector2f p)
    {
        // Compute the cross product: if p is to the left of ab, it is inside.
        return Cross(b - a, p - a) >= 0;
    }

    // Computes the centroid (center of mass) of a polygon.
    public static Vector2f ComputeCentroid(List<Vector2f> polygon)
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
        if (Math.Abs(accumulatedArea) < 1e-7f)
            return polygon[0];
        accumulatedArea *= 3f;
        return new Vector2f(centerX / accumulatedArea, centerY / accumulatedArea);
    }

    // This function computes the actual contact point between two rotated rectangles.
    // It clips rectangle A's corners against rectangle B and computes the centroid of the intersection polygon.
    public static void UpdateContactPoint(ref Manifold m)
    {
        List<Vector2f> polyA = GetRectangleCorners(m.A);
        List<Vector2f> polyB = GetRectangleCorners(m.B);

        List<Vector2f> intersection = SutherlandHodgmanClip(polyA, polyB);
        if (intersection.Count == 0)
        {
            // Fallback: use midpoint between centers.
            m.ContactPoint = (m.A.Center + m.B.Center) * 0.5f;
        }
        else
        {
            m.ContactPoint = ComputeCentroid(intersection);
            m.A.LastContactPoint = m.ContactPoint;
            m.B.LastContactPoint = m.ContactPoint;
        }
    }

    // Helper: 2D cross product returning a scalar.
    public static float Cross(Vector2f a, Vector2f b)
    {
        return a.X * b.Y - a.Y * b.X;
    }
}
