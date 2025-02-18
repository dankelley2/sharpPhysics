using SFML.System;
using physics.Engine.Structs;
using System;

namespace physics.Engine.Shapes
{
    public class BoxPhysShape : IShape
    {
        public float Width { get; }
        public float Height { get; }

        public BoxPhysShape(float width, float height)
        {
            Width = width;
            Height = height;
        }

        public AABB GetAABB(Vector2f center, float angle)
        {
            if (angle == 0)
            {
                return new AABB
                {
                    Min = new Vector2f(center.X - Width / 2, center.Y - Height / 2),
                    Max = new Vector2f(center.X + Width / 2, center.Y + Height / 2)
                };
            }

            float cos = Math.Abs((float)Math.Cos(angle));
            float sin = Math.Abs((float)Math.Sin(angle));
            float newWidth = Width * cos + Height * sin;
            float newHeight = Width * sin + Height * cos;
            return new AABB
            {
                Min = new Vector2f(center.X - newWidth / 2, center.Y - newHeight / 2),
                Max = new Vector2f(center.X + newWidth / 2, center.Y + newHeight / 2)
            };
        }

        public float GetArea() => Width * Height;

        public float GetMomentOfInertia(float mass)
        {
            return (mass / 12f) * (Width * Width + Height * Height);
        }

        public bool Contains(Vector2f point, Vector2f center, float angle)
        {
            // Translate point to local space relative to center.
            Vector2f localPoint = point - center;

            // Rotate the point by -angle to remove the rotation of the box.
            float cos = (float)Math.Cos(-angle);
            float sin = (float)Math.Sin(-angle);
            float localX = localPoint.X * cos - localPoint.Y * sin;
            float localY = localPoint.X * sin + localPoint.Y * cos;

            // Check against unrotated box extents.
            return Math.Abs(localX) <= Width / 2 && Math.Abs(localY) <= Height / 2;
        }

        public float GetWidth()
        {
            return Width;
        }

        public float GetHeight()
        {
            return Height;
        }
    }
}
