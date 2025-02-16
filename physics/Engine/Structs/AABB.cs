using SFML.System;
using physics.Engine.Classes;
using physics.Engine.Helpers;

namespace physics.Engine.Structs
{
    /// <summary>
    /// Axis Aligned bounding box struct that represents the position of an object within a coordinate system.
    /// </summary>
    public struct AABB
    {
        public Vector2f Min;
        public Vector2f Max;
        public float Area => (Max.X - Min.X) * (Max.Y - Min.Y);

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
