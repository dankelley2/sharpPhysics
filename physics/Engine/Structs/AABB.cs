using SFML.System;
using physics.Engine.Classes;
using physics.Engine.Helpers;

namespace physics.Engine.Structs
{
    /// <summary>
    /// Axis Aligned bounding box struct that represents the position of an object within a coordinate system.
    /// </summary>
    public record AABB
    {
        public Vector2f Min;
        public Vector2f Max;
        public float Area => (Max.X - Min.X) * (Max.Y - Min.Y);
    }
}
