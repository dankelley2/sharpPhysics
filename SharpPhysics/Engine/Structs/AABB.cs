#nullable enable
using System.Numerics;

namespace SharpPhysics.Engine.Structs;

/// <summary>
/// Axis-aligned bounding box in world space.
/// </summary>
public struct AABB
{
    public Vector3 Min;
    public Vector3 Max;
    public readonly float Volume => (Max.X - Min.X) * (Max.Y - Min.Y) * (Max.Z - Min.Z);
}
