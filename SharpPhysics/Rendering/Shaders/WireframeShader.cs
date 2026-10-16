#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using SFML.Graphics;
using SharpPhysics.Engine.Helpers;
using SharpPhysics.Engine.Objects;
using SharpPhysics.Engine.Shapes;

namespace SharpPhysics.Rendering.Shaders;

/// <summary>
/// Draws the object's edges as depth-tested lines, optionally with contact normals.
/// </summary>
public class WireframeShader : ObjectShader
{
    private const int CircleSegments = 24;
    private const float NormalLength = 10f;

    private readonly List<Vector3> _points = new();

    public static bool DrawNormals { get; set; } = false;

    public Color Color { get; set; } = Color.White;

    public override void Draw(PhysicsObject obj, SceneRenderer scene)
    {
        _points.Clear();
        AppendEdges(obj.Shape, obj.Center, obj.Orientation);
        scene.DrawLines(CollectionsMarshal.AsSpan(_points), Color);

        if (!DrawNormals)
            return;

        foreach (var (contactPoint, normal) in obj.GetContacts().Values)
            scene.DrawLine(contactPoint, contactPoint + normal * NormalLength, Color.Yellow);
    }

    private void AppendEdges(IShape shape, Vector3 center, Quaternion orientation)
    {
        switch (shape)
        {
            case PolyhedronPhysShape poly:
                var vertices = poly.GetTransformedVertices(center, orientation);
                foreach (var (a, b) in poly.Edges)
                {
                    _points.Add(vertices[a]);
                    _points.Add(vertices[b]);
                }
                break;

            case SpherePhysShape sphere:
                AppendCircle(center, sphere.Radius, orientation, Vector3.UnitX, Vector3.UnitY);
                AppendCircle(center, sphere.Radius, orientation, Vector3.UnitY, Vector3.UnitZ);
                AppendCircle(center, sphere.Radius, orientation, Vector3.UnitZ, Vector3.UnitX);
                break;

            case CompoundShape compound:
                for (int i = 0; i < compound.Children.Count; i++)
                {
                    var (childCenter, childOrientation) = compound.GetChildWorldTransform(i, center, orientation);
                    AppendEdges(compound.Children[i].Shape, childCenter, childOrientation);
                }
                break;
        }
    }

    private void AppendCircle(Vector3 center, float radius, Quaternion orientation, Vector3 axisU, Vector3 axisV)
    {
        Vector3 u = PhysMath.Rotate(axisU, orientation) * radius;
        Vector3 v = PhysMath.Rotate(axisV, orientation) * radius;
        for (int i = 0; i < CircleSegments; i++)
        {
            float t0 = 2f * MathF.PI * i / CircleSegments;
            float t1 = 2f * MathF.PI * (i + 1) / CircleSegments;
            _points.Add(center + u * MathF.Cos(t0) + v * MathF.Sin(t0));
            _points.Add(center + u * MathF.Cos(t1) + v * MathF.Sin(t1));
        }
    }
}
