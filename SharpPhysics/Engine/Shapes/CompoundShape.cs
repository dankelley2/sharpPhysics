#nullable enable
using System.Collections.Generic;
using System.Numerics;
using SharpPhysics.Engine.Helpers;
using SharpPhysics.Engine.Structs;

namespace SharpPhysics.Engine.Shapes;

/// <summary>
/// A child shape within a compound, positioned relative to the compound's center of mass.
/// </summary>
public readonly struct ChildShape
{
    public IShape Shape { get; }
    public Vector3 LocalOffset { get; }
    public Quaternion LocalOrientation { get; }
    public float Mass { get; }
    /// <summary>Inertia tensor in the child's own local frame.</summary>
    public Mat3 Inertia { get; }

    public ChildShape(IShape shape, Vector3 localOffset, Quaternion localOrientation, float mass)
    {
        Shape = shape;
        LocalOffset = localOffset;
        LocalOrientation = localOrientation;
        Mass = mass;
        Inertia = shape.GetInertiaTensor(mass);
    }
}

/// <summary>
/// Multiple child shapes that move as one rigid body.
/// Child offsets must be relative to the compound's center of mass.
/// </summary>
public class CompoundShape : IShape
{
    private readonly List<ChildShape> _children = new();
    private Mat3 _totalInertia;

    public ShapeTypeEnum ShapeType => ShapeTypeEnum.Compound;

    public IReadOnlyList<ChildShape> Children => _children;

    public float TotalMass { get; private set; }

    public void AddChild(IShape shape, Vector3 localOffset, Quaternion localOrientation, float mass)
    {
        var child = new ChildShape(shape, localOffset, localOrientation, mass);
        _children.Add(child);

        var rotation = Mat3.FromQuaternion(localOrientation);
        var inertiaInCompoundFrame = rotation * child.Inertia * rotation.Transpose();

        TotalMass += mass;
        _totalInertia += PhysMath.ParallelAxis(inertiaInCompoundFrame, mass, localOffset);
    }

    public AABB GetAABB(Vector3 center, Quaternion orientation)
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);

        for (int i = 0; i < _children.Count; i++)
        {
            var (childCenter, childOrientation) = GetChildWorldTransform(i, center, orientation);
            var childAabb = _children[i].Shape.GetAABB(childCenter, childOrientation);
            min = Vector3.Min(min, childAabb.Min);
            max = Vector3.Max(max, childAabb.Max);
        }

        return new AABB { Min = min, Max = max };
    }

    public float GetVolume()
    {
        float total = 0f;
        foreach (var child in _children)
            total += child.Shape.GetVolume();
        return total;
    }

    /// <summary>
    /// Returns the aggregated inertia of all children; <paramref name="mass"/> is ignored.
    /// </summary>
    public Mat3 GetInertiaTensor(float mass) => _totalInertia;

    public bool Contains(Vector3 point, Vector3 center, Quaternion orientation)
    {
        for (int i = 0; i < _children.Count; i++)
        {
            var (childCenter, childOrientation) = GetChildWorldTransform(i, center, orientation);
            if (_children[i].Shape.Contains(point, childCenter, childOrientation))
                return true;
        }
        return false;
    }

    public (Vector3 center, Quaternion orientation) GetChildWorldTransform(int childIndex, Vector3 compoundCenter, Quaternion compoundOrientation)
    {
        var child = _children[childIndex];
        return (
            compoundCenter + PhysMath.Rotate(child.LocalOffset, compoundOrientation),
            Quaternion.Concatenate(child.LocalOrientation, compoundOrientation));
    }
}
