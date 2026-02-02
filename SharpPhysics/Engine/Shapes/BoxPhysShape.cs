using System.Numerics;
using SharpPhysics.Engine.Structs;
using System;
using System.Collections.Generic;

namespace SharpPhysics.Engine.Shapes
{
    public class BoxPhysShape : IShape
    {
        public ShapeTypeEnum ShapeType => ShapeTypeEnum.Box;
        public float Width { get; }
        public float Height { get; }
        public List<Vector2> LocalVertices { get; set; } = new List<Vector2>();

        public BoxPhysShape(float width, float height)
        {
            Width = width;
            Height = height;


            // Build LocalVertices (centered at (0,0)).
            // We'll assume the box is centered in local space, so half extents:
            float hw = Width / 2f;
            float hh = Height / 2f;

            // Clockwise corners around (0,0):
            LocalVertices.Add(new Vector2(hw, -hh));
            LocalVertices.Add(new Vector2(-hw, -hh));
            LocalVertices.Add(new Vector2(-hw, hh));
            LocalVertices.Add(new Vector2(hw, hh));
        }

        public AABB GetAABB(Vector2 center, float angle)
        {
            if (angle == 0)
            {
                return new AABB
                {
                    Min = new Vector2(center.X - Width / 2, center.Y - Height / 2),
                    Max = new Vector2(center.X + Width / 2, center.Y + Height / 2)
                };
            }

            float cos = Math.Abs((float)Math.Cos(angle));
            float sin = Math.Abs((float)Math.Sin(angle));
            float newWidth = Width * cos + Height * sin;
            float newHeight = Width * sin + Height * cos;
            return new AABB
            {
                Min = new Vector2(center.X - newWidth / 2, center.Y - newHeight / 2),
                Max = new Vector2(center.X + newWidth / 2, center.Y + newHeight / 2)
            };
        }

        public float GetArea() => Width * Height;

        public float GetMomentOfInertia(float mass)
        {
            return (mass / 12f) * (Width * Width + Height * Height);
        }

        public bool Contains(Vector2 point, Vector2 center, float angle)
        {
            // Translate point to local space relative to center.
            Vector2 localPoint = point - center;

            // Rotate the point by -angle to remove the rotation of the box.
            float cos = (float)Math.Cos(-angle);
            float sin = (float)Math.Sin(-angle);
            float localX = localPoint.X * cos - localPoint.Y * sin;
            float localY = localPoint.X * sin + localPoint.Y * cos;

            // Check against unrotated box extents.
            return Math.Abs(localX) <= Width / 2 && Math.Abs(localY) <= Height / 2;
        }

        /// <summary>
        /// Gets the local point of a point in world space.
        /// </summary>
        /// <param name="point"></param>
        /// <param name="center"></param>
        /// <param name="angle"></param>
        /// <returns></returns>
        public Vector2 GetLocalPoint(Vector2 point, Vector2 center, float angle)
        {
            // Translate point to local space relative to center.
            Vector2 localPoint = point - center;
            // Rotate the point by -angle to remove the rotation of the box.
            float cos = (float)Math.Cos(-angle);
            float sin = (float)Math.Sin(-angle);
            float localX = localPoint.X * cos - localPoint.Y * sin;
            float localY = localPoint.X * sin + localPoint.Y * cos;
            return new Vector2(localX, localY);
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
