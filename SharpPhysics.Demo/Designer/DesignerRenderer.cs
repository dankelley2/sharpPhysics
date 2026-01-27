#nullable enable
using System.Numerics;
using physics.Engine.Rendering;
using SharpPhysics.Demo.Helpers;
using SFML.Graphics;
using SFML.System;

namespace SharpPhysics.Demo.Designer;

/// <summary>
/// Handles all drawing operations for the prefab designer, using cached SFML shapes to reduce GC pressure.
/// </summary>
public class DesignerRenderer : IDisposable
{
    // Cached SFML shapes to avoid GC pressure
    private readonly RectangleShape _cachedLine = new();
    private readonly CircleShape _cachedCircle = new();
    private readonly RectangleShape _cachedRect = new();
    private readonly ConvexShape _cachedConvex = new();
    private readonly RectangleShape _cachedToolbarBg = new();

    // Grid settings
    private readonly Color _gridColor = new(60, 60, 70);
    private readonly Color _gridMajorColor = new(80, 80, 100);

    public DesignerRenderer(uint windowWidth, int toolbarHeight)
    {
        _cachedToolbarBg.Size = new Vector2f(windowWidth, toolbarHeight);
        _cachedToolbarBg.FillColor = new Color(30, 30, 40);
    }

    #region Basic Shape Drawing

    public void DrawLine(Renderer renderer, Vector2 start, Vector2 end, Color color, float thickness)
    {
        var direction = end - start;
        var length = direction.Length();
        if (length < 0.001f) return;

        var angle = MathF.Atan2(direction.Y, direction.X) * 180f / MathF.PI;

        _cachedLine.Size = new Vector2f(length, thickness);
        _cachedLine.Position = new Vector2f(start.X, start.Y);
        _cachedLine.FillColor = color;
        _cachedLine.Rotation = angle;
        _cachedLine.Origin = new Vector2f(0, thickness / 2);

        renderer.Window.Draw(_cachedLine);
    }

    public void DrawCircle(Renderer renderer, Vector2 center, float radius, Color fillColor, Color outlineColor, float outlineThickness)
    {
        _cachedCircle.Radius = radius;
        _cachedCircle.Position = new Vector2f(center.X - radius, center.Y - radius);
        _cachedCircle.FillColor = fillColor;
        _cachedCircle.OutlineColor = outlineColor;
        _cachedCircle.OutlineThickness = outlineThickness;

        renderer.Window.Draw(_cachedCircle);
    }

    public void DrawRectangle(Renderer renderer, Vector2 position, Vector2 size, Color fillColor, Color outlineColor, float outlineThickness)
    {
        _cachedRect.Size = new Vector2f(size.X, size.Y);
        _cachedRect.Position = new Vector2f(position.X, position.Y);
        _cachedRect.FillColor = fillColor;
        _cachedRect.OutlineColor = outlineColor;
        _cachedRect.OutlineThickness = outlineThickness;

        renderer.Window.Draw(_cachedRect);
    }

    public void DrawPolygon(Renderer renderer, Vector2[] points, Color fillColor, Color outlineColor, float outlineThickness)
    {
        if (points.Length < 3) return;

        _cachedConvex.SetPointCount((uint)points.Length);
        for (int i = 0; i < points.Length; i++)
        {
            _cachedConvex.SetPoint((uint)i, new Vector2f(points[i].X, points[i].Y));
        }
        _cachedConvex.FillColor = fillColor;
        _cachedConvex.OutlineColor = outlineColor;
        _cachedConvex.OutlineThickness = outlineThickness;

        renderer.Window.Draw(_cachedConvex);
    }

    #endregion

    #region Grid Drawing

    public void DrawGrid(Renderer renderer, uint width, uint height, int gridSize, int toolbarHeight)
    {
        renderer.Window.SetView(renderer.GameView);

        // Draw vertical lines
        for (int x = 0; x <= width; x += gridSize)
        {
            Color color = (x % (gridSize * 5) == 0) ? _gridMajorColor : _gridColor;
            DrawLine(renderer, new Vector2(x, toolbarHeight), new Vector2(x, height), color, 1f);
        }

        // Draw horizontal lines - start at toolbarHeight and align to grid
        for (int y = toolbarHeight; y <= height; y += gridSize)
        {
            Color color = ((y - toolbarHeight) % (gridSize * 5) == 0) ? _gridMajorColor : _gridColor;
            DrawLine(renderer, new Vector2(0, y), new Vector2(width, y), color, 1f);
        }
    }

    #endregion

    #region Shape Drawing

    public void DrawShape(Renderer renderer, PrefabShape shape)
    {
        switch (shape.Type)
        {
            case ShapeType.Polygon when shape.Points is { Length: >= 3 }:
                DrawPolygon(renderer, shape.Points, 
                    new Color(100, 200, 100, 50), 
                    new Color(100, 200, 100), 2f);
                break;

            case ShapeType.Circle:
                DrawCircle(renderer, shape.Center, shape.Radius,
                    new Color(100, 150, 200, 50),
                    new Color(100, 150, 200), 2f);
                break;

            case ShapeType.Rectangle:
                DrawRectangle(renderer, shape.Position,
                    new Vector2(shape.Width, shape.Height),
                    new Color(200, 150, 100, 50),
                    new Color(200, 150, 100), 2f);
                break;
        }
    }

    public void DrawShapes(Renderer renderer, IReadOnlyList<PrefabShape> shapes)
    {
        renderer.Window.SetView(renderer.GameView);

        foreach (var shape in shapes)
        {
            DrawShape(renderer, shape);
        }
    }

    public void DrawShapeHighlight(Renderer renderer, PrefabShape shape, Color highlightColor)
    {
        switch (shape.Type)
        {
            case ShapeType.Circle:
                DrawCircle(renderer, shape.Center, shape.Radius + 3, highlightColor, highlightColor, 3f);
                break;

            case ShapeType.Rectangle:
                DrawRectangle(renderer, shape.Position - new Vector2(3, 3),
                    new Vector2(shape.Width + 6, shape.Height + 6),
                    new Color(0, 0, 0, 0), highlightColor, 3f);
                break;

            case ShapeType.Polygon when shape.Points is { Length: >= 3 }:
                for (int i = 0; i < shape.Points.Length; i++)
                {
                    int next = (i + 1) % shape.Points.Length;
                    DrawLine(renderer, shape.Points[i], shape.Points[next], highlightColor, 3f);
                }
                break;
        }
    }

    #endregion

    #region Constraint Drawing

    public void DrawConstraints(Renderer renderer, IReadOnlyList<PrefabConstraint> constraints, IReadOnlyList<PrefabShape> shapes)
    {
        foreach (var constraint in constraints)
        {
            if (constraint.ShapeIndexA >= shapes.Count || constraint.ShapeIndexB >= shapes.Count)
                continue;

            DrawConstraint(renderer, constraint, shapes);
        }
    }

    private void DrawConstraint(Renderer renderer, PrefabConstraint constraint, IReadOnlyList<PrefabShape> shapes)
    {
        var shapeA = shapes[constraint.ShapeIndexA];
        var shapeB = shapes[constraint.ShapeIndexB];
        Vector2 centerA = ShapeGeometry.GetShapeCenter(shapeA);
        Vector2 centerB = ShapeGeometry.GetShapeCenter(shapeB);
        Vector2 pivot = constraint.AnchorA;

        Color colorA = constraint.Type == ConstraintType.Weld
            ? new Color(255, 100, 100, 200)  // Red for weld
            : new Color(100, 200, 255, 200); // Cyan for axis (object A - the anchor/base)

        Color colorB = constraint.Type == ConstraintType.Weld
            ? new Color(255, 100, 100, 200)  // Red for weld
            : new Color(100, 255, 100, 200); // Green for axis (object B - the rotating part)

        // Draw lines from each shape center to the pivot point
        DrawLine(renderer, centerA, pivot, colorA, 2f);
        DrawLine(renderer, centerB, pivot, colorB, 2f);

        // Draw the pivot point
        float pivotRadius = constraint.Type == ConstraintType.Weld ? 5f : 8f;
        Color pivotColor = constraint.Type == ConstraintType.Weld
            ? new Color(255, 150, 150, 255)
            : new Color(255, 255, 100, 255); // Yellow pivot for axis
        DrawCircle(renderer, pivot, pivotRadius, pivotColor, Color.White, 2f);

        // For axis constraints, draw rotation indicator around pivot
        if (constraint.Type == ConstraintType.Axis)
        {
            DrawCircle(renderer, pivot, 12f, new Color(0, 0, 0, 0), colorB, 2f);

            // Draw small arrow/indicator on B's line to show it rotates
            Vector2 dirB = Vector2.Normalize(centerB - pivot);
            Vector2 perpB = new Vector2(-dirB.Y, dirB.X);
            Vector2 arrowPos = pivot + dirB * 20f;
            DrawLine(renderer, arrowPos, arrowPos + perpB * 8f, colorB, 2f);
            DrawLine(renderer, arrowPos, arrowPos - perpB * 8f, colorB, 2f);
        }

        // Draw shape index labels near the constraint lines (convert world to screen coords)
        Vector2 labelWorldA = Vector2.Lerp(centerA, pivot, 0.3f);
        Vector2 labelWorldB = Vector2.Lerp(centerB, pivot, 0.3f);
        var screenPosA = renderer.Window.MapCoordsToPixel(new Vector2f(labelWorldA.X, labelWorldA.Y), renderer.GameView);
        var screenPosB = renderer.Window.MapCoordsToPixel(new Vector2f(labelWorldB.X, labelWorldB.Y), renderer.GameView);
        renderer.DrawText($"[{constraint.ShapeIndexA}]", screenPosA.X - 10, screenPosA.Y - 8, 12, colorA);
        renderer.DrawText($"[{constraint.ShapeIndexB}]", screenPosB.X - 10, screenPosB.Y - 8, 12, colorB);

        // Restore GameView after DrawText (which switches to UiView internally)
        renderer.Window.SetView(renderer.GameView);
    }

    #endregion

    #region Preview Drawing

    public void DrawCursorIndicator(Renderer renderer, Vector2 position)
    {
        DrawCircle(renderer, position, 4, Color.Yellow, Color.White, 1f);
    }

    public void DrawPolygonPreview(Renderer renderer, IReadOnlyList<Vector2> points, Vector2 snappedMouse, int gridSize)
    {
        if (points.Count == 0) return;

        // Draw existing points and lines
        for (int i = 0; i < points.Count; i++)
        {
            // Draw point
            DrawCircle(renderer, points[i], 5,
                i == 0 ? Color.Green : Color.Cyan, Color.White, 1f);

            // Draw line to next point
            if (i < points.Count - 1)
            {
                DrawLine(renderer, points[i], points[i + 1], new Color(100, 200, 100), 2f);
            }
        }

        // Draw line from last point to mouse
        DrawLine(renderer, points[^1], snappedMouse, new Color(100, 200, 100, 128), 2f);

        // If close to first point, highlight it
        if (points.Count >= 3)
        {
            float distToFirst = Vector2.Distance(snappedMouse, points[0]);
            if (distToFirst < gridSize)
            {
                DrawCircle(renderer, points[0], 8, new Color(0, 255, 0, 100), Color.Green, 2f);
            }
        }
    }

    public void DrawCirclePreview(Renderer renderer, Vector2 start, Vector2 end)
    {
        float size = Math.Max(Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y));
        Vector2 center = start + new Vector2(size / 2, size / 2);
        float radius = size / 2;

        DrawCircle(renderer, center, radius,
            new Color(100, 150, 200, 50),
            new Color(100, 150, 200, 200), 2f);
    }

    public void DrawRectanglePreview(Renderer renderer, Vector2 start, Vector2 end)
    {
        float minX = Math.Min(start.X, end.X);
        float minY = Math.Min(start.Y, end.Y);
        float width = Math.Abs(end.X - start.X);
        float height = Math.Abs(end.Y - start.Y);

        DrawRectangle(renderer,
            new Vector2(minX, minY),
            new Vector2(width, height),
            new Color(200, 150, 100, 50),
            new Color(200, 150, 100, 200), 2f);
    }

    #endregion

    #region Toolbar Drawing

    public void DrawToolbarBackground(Renderer renderer)
    {
        renderer.Window.SetView(renderer.UiView);
        _cachedToolbarBg.Position = new Vector2f(0, 0);
        renderer.Window.Draw(_cachedToolbarBg);
    }

    #endregion

    public void Dispose()
    {
        _cachedLine.Dispose();
        _cachedCircle.Dispose();
        _cachedRect.Dispose();
        _cachedConvex.Dispose();
        _cachedToolbarBg.Dispose();
    }
}
