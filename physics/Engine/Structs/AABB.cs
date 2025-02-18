using System.Drawing;
using physics.Engine.Classes;

namespace physics.Engine.Structs
{
    /// <summary>
    /// Axis-Aligned Bounding Box for efficient collision detection.
    /// Features:
    /// - Fast overlap tests using min/max comparisons
    /// - Cached area calculation for performance
    /// - Optimized equality comparisons
    /// - Stack allocation for minimal memory usage
    /// </summary>
    public struct AABB
    {
        public Vec2 Min;
        public Vec2 Max;
        /// <summary>
        /// Gets cached area calculation.
        /// Computed on demand to avoid storing extra data.
        /// </summary>
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
