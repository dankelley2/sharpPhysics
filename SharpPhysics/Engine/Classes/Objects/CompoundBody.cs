#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using SharpPhysics.Engine.Helpers;
using SharpPhysics.Engine.Shapes;
using SharpPhysics.Rendering.Shaders;

namespace SharpPhysics.Engine.Objects;

/// <summary>
/// Represents a compound physics body made up of multiple convex shapes
/// that move as a single rigid body. This eliminates the need for internal
/// constraints and provides stable simulation for concave polygons.
/// </summary>
public class CompoundBody : PhysicsObject
{
    /// <summary>
    /// The compound shape containing all child shapes.
    /// </summary>
    public new CompoundShape Shape => (CompoundShape)base.Shape;

    /// <summary>
    /// Gets the number of child shapes in this compound body.
    /// </summary>
    public int ChildCount => Shape.Children.Count;

    public CompoundBody(CompoundShape shape, Vector2 center, float restitution, bool locked, SFMLShader shader, bool canRotate = true)
        : base(shape, center, restitution, locked, shader, 0, canRotate)
    {
        // Mass is calculated from the compound shape's children
        RecalculateMassProperties();
    }

    /// <summary>
    /// Creates a compound body from multiple convex polygon vertices.
    /// Each polygon is converted to a child shape with proper local offset.
    /// </summary>
    public static CompoundBody FromConvexPieces(
        Vector2 worldCenter,
        List<Vector2[]> convexPieces,
        SFMLShader shader,
        bool canRotate = true,
        float restitution = 0.2f)
    {
        var compoundShape = new CompoundShape();

        // Calculate the overall centroid of all pieces combined
        Vector2 overallCentroid = ComputeOverallCentroid(convexPieces);

        foreach (var pieceVertices in convexPieces)
        {
            // Create a polygon shape for this piece
            var polygonShape = new PolygonPhysShape(pieceVertices);

            // Calculate this piece's centroid
            Vector2 pieceCentroid = CollisionHelpers.ComputeCentroid(new List<Vector2>(pieceVertices));

            // Local offset is relative to the compound's center of mass
            Vector2 localOffset = pieceCentroid - overallCentroid;

            // Calculate mass based on area
            float mass = polygonShape.GetArea();

            compoundShape.AddChild(polygonShape, localOffset, 0f, mass);
        }

        return new CompoundBody(compoundShape, worldCenter, restitution, false, shader, canRotate);
    }

    /// <summary>
    /// Creates a compound body from multiple convex polygon vertices.
    /// Each polygon is converted to a child shape with proper local offset.
    /// </summary>
    public static CompoundBody FromPhysicsObjects(
        Vector2 worldCenter,
        List<PhysicsObject> physicsObjects,
        SFMLShader shader,
        bool canRotate = true,
        float restitution = 0.2f)
    {
        var compoundShape = new CompoundShape();

        // Calculate the overall centroid of all pieces combined
        Vector2 overallCentroid = ComputeOverallCentroid(physicsObjects);

        foreach (var piece in physicsObjects)
        {
            // Local offset is relative to the compound's center of mass
            Vector2 localOffset = piece.Center - overallCentroid;

            compoundShape.AddChild(piece.Shape, localOffset, 0f, piece.Mass);
        }

        return new CompoundBody(compoundShape, worldCenter, restitution, false, shader, canRotate);
    }

    /// <summary>
    /// Computes the overall centroid of multiple convex pieces weighted by area.
    /// </summary>
    private static Vector2 ComputeOverallCentroid(List<Vector2[]> convexPieces)
    {
        Vector2 weightedSum = Vector2.Zero;
        float totalArea = 0f;

        foreach (var piece in convexPieces)
        {
            var centroid = CollisionHelpers.ComputeCentroid(new List<Vector2>(piece));
            float area = ComputePolygonArea(piece);
            weightedSum += centroid * area;
            totalArea += area;
        }

        return totalArea > 0 ? weightedSum / totalArea : Vector2.Zero;
    }

    /// <summary>
    /// Computes the overall centroid of multiple convex pieces weighted by area.
    /// </summary>
    private static Vector2 ComputeOverallCentroid(List<PhysicsObject> pieces)
    {
        Vector2 weightedSum = Vector2.Zero;
        float totalArea = 0f;

        foreach (var piece in pieces)
        {
            var centroid = piece.Center;
            float area = piece.Mass;
            weightedSum += centroid * area;
            totalArea += area;
        }

        return totalArea > 0 ? weightedSum / totalArea : Vector2.Zero;
    }

    /// <summary>
    /// Computes the area of a polygon using the shoelace formula.
    /// </summary>
    private static float ComputePolygonArea(Vector2[] vertices)
    {
        float area = 0f;
        int n = vertices.Length;
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            area += vertices[i].X * vertices[j].Y;
            area -= vertices[j].X * vertices[i].Y;
        }
        return Math.Abs(area) / 2f;
    }

    /// <summary>
    /// Recalculates mass and inertia from child shapes.
    /// </summary>
    private void RecalculateMassProperties()
    {
        float totalMass = 0f;
        float totalInertia = 0f;

        foreach (var child in Shape.Children)
        {
            totalMass += child.Mass;
            // Parallel axis theorem: I = I_cm + m*d^2
            totalInertia += child.Inertia + child.Mass * Vector2.Dot(child.LocalOffset, child.LocalOffset);
        }

        // Use protected setters from base class
        Mass = totalMass;
        IMass = totalMass > 0 ? 1f / totalMass : 0f;
        Inertia = totalInertia;
        IInertia = totalInertia > 0 ? 1f / totalInertia : 0f;
    }

    /// <summary>
    /// Gets the world-space transform for a specific child shape.
    /// </summary>
    public (Vector2 center, float angle) GetChildWorldTransform(int childIndex)
    {
        return Shape.GetChildWorldTransform(childIndex, Center, Angle);
    }

    /// <summary>
    /// Applies a force at a world point, affecting both linear and angular velocity.
    /// </summary>
    public void ApplyForceAtPoint(Vector2 force, Vector2 worldPoint)
    {
        if (Locked)
            return;

        // Linear component
        Velocity += force * IMass;

        // Angular component (torque = r × F)
        Vector2 r = worldPoint - Center;
        float torque = PhysMath.Cross(r, force);
        AngularVelocity += torque * IInertia;
    }

    /// <summary>
    /// Applies an impulse at a world point, affecting both linear and angular velocity.
    /// </summary>
    public void ApplyImpulseAtPoint(Vector2 impulse, Vector2 worldPoint)
    {
        if (Locked)
            return;

        // Linear component
        Velocity += impulse * IMass;

        // Angular component
        Vector2 r = worldPoint - Center;
        float angularImpulse = PhysMath.Cross(r, impulse);
        AngularVelocity += angularImpulse * IInertia;
    }
}

