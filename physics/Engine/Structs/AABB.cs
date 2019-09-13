using System.Drawing;
using physics.Engine.Classes;

namespace physics.Engine.Structs
{
    /// <summary>
    /// Axis Aligned bounding box struct that represents the position of an object within a coordinate system.
    /// </summary>
    public struct AABB
    {
        public Vec2 Min;
        public Vec2 Max;

        public static bool operator ==(AABB left, AABB right)
        {
            return left.Min == right.Min && left.Max == right.Max;
        }

        public static bool operator !=(AABB left, AABB right)
        {
            return left.Min != right.Min || left.Max != right.Max;
        }

    }
}