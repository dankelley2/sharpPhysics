#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using SharpPhysics.Engine.Structs;

namespace SharpPhysics.Engine.Shapes;

/// <summary>
/// Represents a child shape within a compound body, storing its local transform relative to the compound's center of mass.
/// </summary>
public readonly struct ChildShape
{
    public IShape Shape { get; }
    public Vector2 LocalOffset { get; }
    public float LocalAngle { get; }
    public float Mass { get; }
    public float Inertia { get; }

    public ChildShape(IShape shape, Vector2 localOffset, float localAngle, float mass)
    {
        Shape = shape;
        LocalOffset = localOffset;
        LocalAngle = localAngle;
        Mass = mass;
        Inertia = shape.GetMomentOfInertia(mass);
    }
}

/// <summary>
/// A compound shape made up of multiple convex child shapes.
/// Used for concave polygon decomposition where all pieces move as one rigid body.
/// </summary>
public class CompoundShape : IShape
{
    public ShapeTypeEnum ShapeType => ShapeTypeEnum.Compound;

    public List<Vector2> LocalVertices { get; set; } = new();

    public List<ChildShape> Children { get; } = new();

    private float _totalMass;
    private float _totalInertia;
    private float _cachedWidth;
    private float _cachedHeight;

    public CompoundShape()
    {
    }

    /// <summary>
    /// Adds a child shape to the compound. The localOffset is relative to the compound's center of mass.
    /// </summary>
    public void AddChild(IShape shape, Vector2 localOffset, float localAngle, float mass)
    {
        var child = new ChildShape(shape, localOffset, localAngle, mass);
        Children.Add(child);
        
        _totalMass += mass;
        // Parallel axis theorem: I_total = I_child + m * d^2
        _totalInertia += child.Inertia + mass * Vector2.Dot(localOffset, localOffset);

        RecalculateBounds();
    }

    /// <summary>
    /// Recalculates the cached bounds after adding children.
    /// </summary>
    private void RecalculateBounds()
    {
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;

        LocalVertices.Clear();

        foreach (var child in Children)
        {
            // Get the child's AABB at its local position within the compound
            var childAabb = child.Shape.GetAABB(child.LocalOffset, child.LocalAngle);
            
            if (childAabb.Min.X < minX) minX = childAabb.Min.X;
            if (childAabb.Max.X > maxX) maxX = childAabb.Max.X;
            if (childAabb.Min.Y < minY) minY = childAabb.Min.Y;
            if (childAabb.Max.Y > maxY) maxY = childAabb.Max.Y;

            // Collect all vertices for collision purposes
            var childVerts = child.Shape.GetTransformedVertices(child.LocalOffset, child.LocalAngle);
            foreach (var v in childVerts)
            {
                LocalVertices.Add(v);
            }
        }

        _cachedWidth = maxX - minX;
        _cachedHeight = maxY - minY;
    }

    public AABB GetAABB(Vector2 center, float angle)
    {
        float cos = (float)Math.Cos(angle);
        float sin = (float)Math.Sin(angle);

        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;

        foreach (var child in Children)
        {
            // Rotate child's local offset by compound's angle
            Vector2 rotatedOffset = new(
                child.LocalOffset.X * cos - child.LocalOffset.Y * sin,
                child.LocalOffset.X * sin + child.LocalOffset.Y * cos
            );
            Vector2 childWorldCenter = center + rotatedOffset;
            float childWorldAngle = angle + child.LocalAngle;

            var childAabb = child.Shape.GetAABB(childWorldCenter, childWorldAngle);

            if (childAabb.Min.X < minX) minX = childAabb.Min.X;
            if (childAabb.Max.X > maxX) maxX = childAabb.Max.X;
            if (childAabb.Min.Y < minY) minY = childAabb.Min.Y;
            if (childAabb.Max.Y > maxY) maxY = childAabb.Max.Y;
        }

        return new AABB { Min = new Vector2(minX, minY), Max = new Vector2(maxX, maxY) };
    }

    public float GetArea()
    {
        float total = 0f;
        foreach (var child in Children)
            total += child.Shape.GetArea();
        return total;
    }

    public float GetWidth() => _cachedWidth;
    public float GetHeight() => _cachedHeight;

    public float GetMomentOfInertia(float mass)
    {
        // Return the pre-calculated total inertia using parallel axis theorem
        return _totalInertia;
    }

    public bool Contains(Vector2 point, Vector2 center, float angle)
    {
        float cos = (float)Math.Cos(angle);
        float sin = (float)Math.Sin(angle);

        foreach (var child in Children)
        {
            // Transform to child's world position
            Vector2 rotatedOffset = new(
                child.LocalOffset.X * cos - child.LocalOffset.Y * sin,
                child.LocalOffset.X * sin + child.LocalOffset.Y * cos
            );
            Vector2 childWorldCenter = center + rotatedOffset;
            float childWorldAngle = angle + child.LocalAngle;

            if (child.Shape.Contains(point, childWorldCenter, childWorldAngle))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Gets all transformed vertices from all child shapes.
    /// </summary>
    public Vector2[] GetTransformedVertices(Vector2 center, float angle)
    {
        var allVerts = new List<Vector2>();
        float cos = (float)Math.Cos(angle);
        float sin = (float)Math.Sin(angle);

        foreach (var child in Children)
        {
            Vector2 rotatedOffset = new(
                child.LocalOffset.X * cos - child.LocalOffset.Y * sin,
                child.LocalOffset.X * sin + child.LocalOffset.Y * cos
            );
            Vector2 childWorldCenter = center + rotatedOffset;
            float childWorldAngle = angle + child.LocalAngle;

            var childVerts = child.Shape.GetTransformedVertices(childWorldCenter, childWorldAngle);
            allVerts.AddRange(childVerts);
        }

        return allVerts.ToArray();
    }

    /// <summary>
    /// Gets the world-space center and angle for a specific child shape.
    /// </summary>
    public (Vector2 center, float angle) GetChildWorldTransform(int childIndex, Vector2 compoundCenter, float compoundAngle)
    {
        var child = Children[childIndex];
        float cos = (float)Math.Cos(compoundAngle);
        float sin = (float)Math.Sin(compoundAngle);

        Vector2 rotatedOffset = new(
            child.LocalOffset.X * cos - child.LocalOffset.Y * sin,
            child.LocalOffset.X * sin + child.LocalOffset.Y * cos
        );

        return (compoundCenter + rotatedOffset, compoundAngle + child.LocalAngle);
    }
}
