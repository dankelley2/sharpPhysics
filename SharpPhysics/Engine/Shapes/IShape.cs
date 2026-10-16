#nullable enable
using System.Numerics;
using SharpPhysics.Engine.Helpers;
using SharpPhysics.Engine.Structs;

namespace SharpPhysics.Engine.Shapes;

/// <summary>
/// A collision shape defined in local space around its center of mass.
/// </summary>
public interface IShape
{
    ShapeTypeEnum ShapeType { get; }

    AABB GetAABB(Vector3 center, Quaternion orientation);

    float GetVolume();

    /// <summary>
    /// Local-space inertia tensor about the shape's center of mass.
    /// </summary>
    Mat3 GetInertiaTensor(float mass);

    bool Contains(Vector3 point, Vector3 center, Quaternion orientation);
}
