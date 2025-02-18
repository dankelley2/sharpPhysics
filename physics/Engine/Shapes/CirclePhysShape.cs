using SFML.System;
using physics.Engine.Structs;
using System;

namespace physics.Engine.Shapes
{
    public class CirclePhysShape : IShape
    {
        public float Radius { get; }

        public CirclePhysShape(float radius)
        {
            Radius = radius;
        }

        public AABB GetAABB(Vector2f center, float angle)
        {
            // A circle's AABB is independent of rotation.
            return new AABB
            {
                Min = new Vector2f(center.X - Radius, center.Y - Radius),
                Max = new Vector2f(center.X + Radius, center.Y + Radius)
            };
        }

        public float GetArea() => (float)(Math.PI * Radius * Radius);

        public float GetMomentOfInertia(float mass)
        {
            return 0.5f * mass * Radius * Radius;
        }

        public bool Contains(Vector2f point, Vector2f center, float angle)
        {
            float dx = point.X - center.X;
            float dy = point.Y - center.Y;
            return (dx * dx + dy * dy) <= (Radius * Radius);
        }

        public float GetWidth()
        {
            return Radius * 2;
        }

        public float GetHeight()
        {
            return Radius * 2;
        }
    }
}
