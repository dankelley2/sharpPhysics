using System.Numerics;
using SharpPhysics.Engine.Structs;
using System;
using System.Collections.Generic;

namespace SharpPhysics.Engine.Shapes
{
    public class CirclePhysShape : IShape
    {
        const int SEGMENTS = 8;

        public ShapeTypeEnum ShapeType => ShapeTypeEnum.Circle;

        public float Radius { get; }
        
        public List<Vector2> LocalVertices { get; set; } = new List<Vector2>();

        public CirclePhysShape(float radius)
        {
            Radius = radius;

            // Build local vertices approximating a circle with 'resolution' points
            // around local (0,0).
            
            // multiples of 20
            var mulitplier = Math.Max(1,(int)radius / 20);

            var segmentCount = SEGMENTS * mulitplier;

            for (int i = 0; i < segmentCount; i++)
            {
                float theta = (2f * (float)Math.PI * i) / segmentCount;
                float x = Radius * (float)Math.Cos(theta);
                float y = Radius * (float)Math.Sin(theta);
                LocalVertices.Add(new Vector2(x, y));
            }
        }

        public AABB GetAABB(Vector2 center, float angle)
        {
            // A circle's AABB is independent of rotation.
            return new AABB
            {
                Min = new Vector2(center.X - Radius, center.Y - Radius),
                Max = new Vector2(center.X + Radius, center.Y + Radius)
            };
        }

        public float GetArea() => (float)(Math.PI * Radius * Radius);

        public float GetMomentOfInertia(float mass)
        {
            return 0.5f * mass * Radius * Radius;
        }

        public bool Contains(Vector2 point, Vector2 center, float angle)
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
