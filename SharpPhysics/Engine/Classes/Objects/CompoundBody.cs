#nullable enable
using System.Numerics;
using SharpPhysics.Engine.Shapes;
using SharpPhysics.Rendering.Shaders;

namespace SharpPhysics.Engine.Objects;

/// <summary>
/// A rigid body made of multiple child shapes. Mass and inertia are aggregated from the children,
/// whose offsets must be relative to the compound's center of mass.
/// </summary>
public class CompoundBody : PhysicsObject
{
    public new CompoundShape Shape => (CompoundShape)base.Shape;

    public int ChildCount => Shape.Children.Count;

    public CompoundBody(CompoundShape shape, Vector3 center, float restitution, bool locked, ObjectShader shader, bool canRotate = true)
        : base(shape, center, restitution, locked, shader, shape.TotalMass, canRotate)
    {
    }

    public (Vector3 center, Quaternion orientation) GetChildWorldTransform(int childIndex)
    {
        return Shape.GetChildWorldTransform(childIndex, Center, Orientation);
    }
}
