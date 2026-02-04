#nullable enable
using SFML.Graphics;
using SFML.System;
using System.Numerics;
using SharpPhysics.Engine.Objects;
using SharpPhysics.Engine.Shapes;
using SharpPhysics.Engine.Helpers;

namespace SharpPhysics.Rendering.Shaders;

/// <summary>
/// Shader for rendering CompoundBody objects by drawing each child shape.
/// </summary>
public class SFMLCompoundShader : SFMLShader
{
    private readonly VertexArray _vertices = new(PrimitiveType.LineStrip);
    // Preallocated VertexArray for drawing contact normals.
    private VertexArray contactLines = new VertexArray(PrimitiveType.Lines);
    private readonly Color _fillColor;
    private readonly Color _outlineColor;
    private static readonly CircleShape Circle = new CircleShape();

    public SFMLCompoundShader(Color? fillColor = null, Color? outlineColor = null)
    {
        _fillColor = fillColor ?? new Color(100, 100, 100, 128);
        _outlineColor = outlineColor ?? Color.White;
    }

    public override void PreDraw(PhysicsObject obj, RenderTarget target)
    {
    }

    public override void Draw(PhysicsObject obj, RenderTarget target)
    {
        if (obj is not CompoundBody compound)
        {
            return;
        }

        var compoundShape = compound.Shape;

        // Draw each child shape
        for (int i = 0; i < compoundShape.Children.Count; i++)
        {
            var (childCenter, childAngle) = compoundShape.GetChildWorldTransform(i, compound.Center, compound.Angle);
            var child = compoundShape.Children[i];

            DrawChildShape(child.Shape, childCenter, childAngle, target);
        }
    }

    private void DrawChildShape(IShape shape, Vector2 center, float angle, RenderTarget target)
    {
        _vertices.Clear();

        if (shape.ShapeType == ShapeTypeEnum.Circle)
        {
            var circle = (CirclePhysShape)shape;
            DrawCircle(center, circle.Radius, target);
            return;
        }

        // Get transformed vertices for polygon shapes
        var points = shape.GetTransformedVertices(center, angle);

        // Add vertices
        for (int i = 0; i < points.Length; i++)
        {
            _vertices.Append(new Vertex(points[i].ToSfml(), _outlineColor));
        }

        // Close the loop
        if (points.Length > 0)
        {
            _vertices.Append(new Vertex(points[0].ToSfml(), _outlineColor));
        }

        target.Draw(_vertices);
    }

    private void DrawCircle(Vector2 center, float radius, RenderTarget target)
    {
        Circle.Radius = radius;
        Circle.Origin = new Vector2f(radius, radius);
        Circle.Position = center.ToSfml();
        Circle.OutlineThickness = 1f;
        Circle.OutlineColor = _outlineColor;
        Circle.FillColor = _fillColor;
        
        target.Draw(Circle);
    }

    public override void PostDraw(PhysicsObject obj, RenderTarget target)
    {
        if (obj is not CompoundBody compound)
        {
            return;
        }

        // Draw center of mass marker
        float radius = 3f;

        Circle.Radius = radius;
        Circle.Origin = new Vector2f(radius, radius);
        Circle.Position = compound.Center.ToSfml();
        Circle.OutlineThickness = 1f;
        Circle.OutlineColor = compound.Sleeping ? Color.Blue : Color.Red;
        Circle.FillColor = Color.Transparent;
        
        target.Draw(Circle);

        // 3) Draw a line from each contact point along its normal.
        contactLines.Clear();
        float lineLength = 10f; // Adjust this value to scale the drawn normals.
        foreach (var kv in obj.GetContacts())
        {
            // kv.Key is the other PhysicsObject (unused here), and kv.Value is (contactPoint, normal).
            (Vector2 contactPoint, Vector2 normal) = kv.Value;
            contactLines.Append(new Vertex(contactPoint.ToSfml(), normal.Y > 0 ? Color.Yellow : Color.Cyan));
            contactLines.Append(new Vertex((contactPoint + normal * lineLength).ToSfml(), normal.Y > 0 ? Color.Yellow : Color.Cyan));
        }
        target.Draw(contactLines);
    }
}
