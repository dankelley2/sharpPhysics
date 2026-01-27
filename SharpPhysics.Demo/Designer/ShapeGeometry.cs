#nullable enable
using System.Numerics;
using SharpPhysics.Demo.Helpers;

namespace SharpPhysics.Demo.Designer;

/// <summary>
/// Static helper methods for shape geometry calculations in the prefab designer.
/// </summary>
public static class ShapeGeometry
{
    /// <summary>
    /// Snaps a point to the grid, accounting for toolbar offset.
    /// </summary>
    /// <param name="point">The point to snap.</param>
    /// <param name="gridSize">The grid cell size.</param>
    /// <param name="toolbarHeight">The toolbar height offset.</param>
    public static Vector2 SnapToGrid(Vector2 point, int gridSize, int toolbarHeight)
    {
        // For X: snap normally since grid starts at X=0
        float snappedX = MathF.Round(point.X / gridSize) * gridSize;

        // For Y: account for the toolbar offset
        // Grid starts at Y=toolbarHeight, so subtract offset, snap, then add back
        float adjustedY = point.Y - toolbarHeight;
        float snappedY = MathF.Round(adjustedY / gridSize) * gridSize + toolbarHeight;

        return new Vector2(snappedX, snappedY);
    }

    /// <summary>
    /// Gets the center point of a shape.
    /// </summary>
    public static Vector2 GetShapeCenter(PrefabShape shape)
    {
        return shape.Type switch
        {
            ShapeType.Circle => shape.Center,
            ShapeType.Rectangle => shape.Position + new Vector2(shape.Width / 2, shape.Height / 2),
            ShapeType.Polygon when shape.Points is { Length: > 0 } => CalculatePolygonCentroid(shape.Points),
            _ => Vector2.Zero
        };
    }

    /// <summary>
    /// Calculates the centroid of a polygon.
    /// </summary>
    public static Vector2 CalculatePolygonCentroid(Vector2[] points)
    {
        if (points.Length == 0)
            return Vector2.Zero;

        Vector2 sum = Vector2.Zero;
        foreach (var pt in points)
            sum += pt;
        return sum / points.Length;
    }

    /// <summary>
    /// Finds the index of the shape at a given point, checking in reverse order (top-most first).
    /// </summary>
    /// <param name="point">The point to check.</param>
    /// <param name="shapes">The list of shapes to search.</param>
    /// <returns>The index of the shape at the point, or null if none found.</returns>
    public static int? FindShapeAtPoint(Vector2 point, IReadOnlyList<PrefabShape> shapes)
    {
        // Check shapes in reverse order (top-most first)
        for (int i = shapes.Count - 1; i >= 0; i--)
        {
            if (IsPointInShape(point, shapes[i]))
            {
                return i;
            }
        }
        return null;
    }

    /// <summary>
    /// Determines if a point is inside a shape.
    /// </summary>
    public static bool IsPointInShape(Vector2 point, PrefabShape shape)
    {
        return shape.Type switch
        {
            ShapeType.Circle => IsPointInCircle(point, shape.Center, shape.Radius),
            ShapeType.Rectangle => IsPointInRectangle(point, shape.Position, shape.Width, shape.Height),
            ShapeType.Polygon when shape.Points is { Length: >= 3 } => IsPointInPolygon(point, shape.Points),
            _ => false
        };
    }

    /// <summary>
    /// Determines if a point is inside a circle.
    /// </summary>
    public static bool IsPointInCircle(Vector2 point, Vector2 center, float radius)
    {
        float dist = Vector2.Distance(point, center);
        return dist <= radius;
    }

    /// <summary>
    /// Determines if a point is inside a rectangle.
    /// </summary>
    public static bool IsPointInRectangle(Vector2 point, Vector2 position, float width, float height)
    {
        return point.X >= position.X &&
               point.X <= position.X + width &&
               point.Y >= position.Y &&
               point.Y <= position.Y + height;
    }

    /// <summary>
    /// Determines if a point is inside a polygon using ray casting algorithm.
    /// </summary>
    public static bool IsPointInPolygon(Vector2 point, Vector2[] polygon)
    {
        if (polygon.Length < 3)
            return false;

        int crossings = 0;
        int n = polygon.Length;

        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            Vector2 vi = polygon[i];
            Vector2 vj = polygon[j];

            if ((vi.Y <= point.Y && vj.Y > point.Y) || (vj.Y <= point.Y && vi.Y > point.Y))
            {
                float t = (point.Y - vi.Y) / (vj.Y - vi.Y);
                float xIntersect = vi.X + t * (vj.X - vi.X);

                if (point.X < xIntersect)
                    crossings++;
            }
        }

        return (crossings % 2) == 1;
    }
}
