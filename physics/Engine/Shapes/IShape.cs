using SFML.System;
using physics.Engine.Structs;

namespace physics.Engine.Shapes
{
    public interface IShape
    {
        /// <summary>
        /// Computes the axis-aligned bounding box for this shape, given a center position and rotation angle (in radians).
        /// </summary>
        AABB GetAABB(Vector2f center, float angle);

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
        bool Contains(Vector2f point, Vector2f center, float angle);

        /// <summary>
        /// Gets the local point of a point in world space.
        /// </summary>
        /// <param name="point"></param>
        /// <param name="center"></param>
        /// <param name="angle"></param>
        /// <returns></returns>
        public Vector2f GetLocalPoint(Vector2f point, Vector2f center, float angle)
        {
            return point - center;
        }
    }
}
