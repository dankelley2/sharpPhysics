using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using physics.Engine.Structs;

namespace physics.Engine.Classes
{
    public class PhysicsObject
    {
        public enum Type
        {
            Box,
            Circle
        }

        public bool Locked;
        public AABB Aabb;
        public Vec2 Velocity;
        public Vec2 Center;
        public Vec2 Pos;
        public float Width;
        public float Height;
        public float Restitution;
        public float Mass;
        public float IMass;

        public PhysicsObject(AABB boundingBox, Type t, float r, float m, bool locked)
        {
            Velocity = new Vec2(0, 0);
            Aabb = boundingBox;
            Width = Aabb.Max.X - Aabb.Min.X;
            Height = Aabb.Max.Y - Aabb.Min.Y;
            Pos = new Vec2(Aabb.Min.X, Aabb.Min.Y);
            Center = new Vec2(Pos.X + Width / 2, Pos.Y + Height / 2);
            ShapeType = t;
            Restitution = r;
            Mass = m;
            IMass = 1 / Mass;
            Locked = locked;
        }

        public Type ShapeType { get; set; }

        public bool Contains(PointF p)
        {
            if (Aabb.Max.X > p.X && p.X > Aabb.Min.X)
            {
                if (Aabb.Max.Y > p.Y && p.Y > Aabb.Min.Y)
                {
                    return true;
                }
            }

            return false;
        }

        public void Move()
        {
            if (Locked)
            {
                return;
            }
            RoundSpeedToZero();

            var p1 = Aabb.Min + Velocity;
            var p2 = Aabb.Max + Velocity;
            Aabb = new AABB { Min = p1, Max = p2 };
            Recalculate();
        }

        private void RoundSpeedToZero()
        {
            if (Math.Abs(this.Velocity.X) + Math.Abs(this.Velocity.Y) < .01F)
            {
                Velocity = new Vec2(0,0);
            }
        }

        private void Recalculate()
        {
            Width = Aabb.Max.X - Aabb.Min.X;
            Height = Aabb.Max.Y - Aabb.Min.Y;
            Pos = new Vec2(Aabb.Min.X, Aabb.Min.Y);
            Center = new Vec2(Pos.X + Width / 2, Pos.Y + Height / 2);
        }

        public void Move(Vec2 dVector)
        {
            if (Locked)
            {
                return;
            }

            var p1 = Aabb.Min + dVector;
            var p2 = Aabb.Max + dVector;
            Aabb = new AABB { Min = p1, Max = p2 };
            Recalculate();
        }
    }
}