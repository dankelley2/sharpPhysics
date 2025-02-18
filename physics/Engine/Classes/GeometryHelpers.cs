using System;
using physics.Engine.Structs;
using SFML.System;

namespace physics.Engine.Classes
{

    public static class GeometryHelpers
    {
        /// <summary>
        /// Given an AABB and a rotation angle (in radians), returns a new AABB that fully contains
        /// the rectangle after it is rotated about its center.
        /// </summary>
        /// <param name="aabb">The original axis-aligned bounding box.</param>
        /// <param name="angle">The rotation angle in radians.</param>
        /// <returns>A new AABB that contains the rotated rectangle.</returns>
        public static AABB GetRotatedAABB(PhysicsObject obj)
        {
            // Calculate the original width and height.
            if (obj.Angle == 0)
            {
                return obj.Aabb;
            }

            float originalWidth = obj.Aabb.Max.X - obj.Aabb.Min.X;
            float originalHeight = obj.Aabb.Max.Y - obj.Aabb.Min.Y;

            // Use absolute values of cosine and sine.
            float cos = Math.Abs((float)Math.Cos(obj.Angle));
            float sin = Math.Abs((float)Math.Sin(obj.Angle));

            // Compute new width and height based on the rotated extents.
            float newWidth = originalWidth * cos + originalHeight * sin;
            float newHeight = originalWidth * sin + originalHeight * cos;

            // Calculate the center of the original AABB.
            Vector2f center = new Vector2f(
                (obj.Aabb.Min.X + obj.Aabb.Max.X) / 2f,
                (obj.Aabb.Min.Y + obj.Aabb.Max.Y) / 2f);

            // Create the new AABB centered at the same point.
            AABB newAABB = new AABB
            {
                Min = new Vector2f(center.X - newWidth / 2f, center.Y - newHeight / 2f),
                Max = new Vector2f(center.X + newWidth / 2f, center.Y + newHeight / 2f)
            };

            return newAABB;
        }
    }
}
