#nullable enable
using System;
using System.Numerics;
using SharpPhysics.Engine.Helpers;
using SharpPhysics.Engine.Structs;

namespace SharpPhysics.Engine.Shapes;

public class SpherePhysShape : IShape
{
    public ShapeTypeEnum ShapeType => ShapeTypeEnum.Sphere;

    public float Radius { get; }

    public SpherePhysShape(float radius)
    {
        Radius = radius;
    }

    public AABB GetAABB(Vector3 center, Quaternion orientation)
    {
        var extent = new Vector3(Radius);
        return new AABB { Min = center - extent, Max = center + extent };
    }

    public float GetVolume() => 4f / 3f * MathF.PI * Radius * Radius * Radius;

    public Mat3 GetInertiaTensor(float mass)
    {
        float i = 0.4f * mass * Radius * Radius;
        return Mat3.Diagonal(i, i, i);
    }

    public bool Contains(Vector3 point, Vector3 center, Quaternion orientation)
    {
        return Vector3.DistanceSquared(point, center) <= Radius * Radius;
    }
}
