using System.Numerics;

namespace SharpPhysics.Engine.Structs
{
    /// <summary>
    /// Axis Aligned bounding box struct that represents the position of an object within a coordinate system.
    /// </summary>
    public struct AABB
    {
        public Vector2 Min;
        public Vector2 Max;
        public readonly float Area => (Max.X - Min.X) * (Max.Y - Min.Y);
    }
}
