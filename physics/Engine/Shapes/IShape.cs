using physics.Engine.Structs;
using System.Collections.Generic;
using System.Numerics;

namespace physics.Engine.Shapes
{
    public interface IShape
    {
        /// <summary>
        /// Computes the axis-aligned bounding box for this shape, given a center position and rotation angle (in radians).
        /// </summary>
        AABB GetAABB(Vector2 center, float angle);

        // Shape Enum so we don't have to run IsInstanceOfClass 100 million times every minute
        public ShapeTypeEnum ShapeType { get; }

        public List<Vector2> LocalVertices { get; set; }

        /// <summary>
        /// Returns the area of the shape.
        /// </summary>
        float GetArea();

        float GetWidth();

        float GetHeight();

        /// <summary>
        /// Returns the moment of inertia for the shape given a mass.
        /// </summary>
        float GetMomentOfInertia(float mass);

        /// <summary>
        /// Determines whether a given point (in world coordinates) lies within the shape, given the shape’s center and rotation.
        /// </summary>
        bool Contains(Vector2 point, Vector2 center, float angle);

        /// <summary>
        /// Gets the local point of a point in world space.
        /// </summary>
        /// <param name="point"></param>
        /// <param name="center"></param>
        /// <param name="angle"></param>
        /// <returns></returns>
        public Vector2 GetLocalPoint(Vector2 point, Vector2 center, float angle)
        {
            return point - center;
        }

        /// <summary>
        /// Returns a list of the shape's vertices transformed into world space,
        /// using the provided center and angle.
        /// </summary>
        public virtual Vector2[] GetTransformedVertices(Vector2 center, float angle)
        {
            var transformed = new Vector2[LocalVertices.Count];

            float cos = (float)System.Math.Cos(angle);
            float sin = (float)System.Math.Sin(angle);

            for(int i = 0; i < LocalVertices.Count; i++)
            {
                var local = LocalVertices[i];
                // Rotate the local vertex
                float rx = local.X * cos - local.Y * sin;
                float ry = local.X * sin + local.Y * cos;

                // Then translate by the object's center
                float worldX = center.X + rx;
                float worldY = center.Y + ry;

                transformed[i] = new Vector2(worldX, worldY);
            }

            return transformed;
        }
    }
}
