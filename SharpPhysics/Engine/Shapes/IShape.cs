using SharpPhysics.Engine.Structs;
using System.Collections.Generic;
using System.Numerics;

namespace SharpPhysics.Engine.Shapes
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
        Vector2[] GetTransformedVertices(Vector2 center, float angle);
    }
}
