using System;
using System.Drawing;
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

        private AABB _aabb;


        private Vec2 _velocity;
        public Vec2 Center;
        public Vec2 Pos;
        public float Width;
        public float Height;
        public float IMass;

        public bool Locked;
        public float Mass;

        public float Restitution;

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

        public AABB Aabb
        {
            get { return _aabb; }
            set
            {
                if (!Locked)
                {
                    _aabb = value;
                }
            }
        }

        public Vec2 Velocity
        {
            get { return _velocity; }
            set
            {
                if (!Locked)
                {
                    _velocity = value;
                }
            }
        }

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

            var p1 = Aabb.Min + Velocity;
            var p2 = Aabb.Max + Velocity;
            Aabb = new AABB { Min = p1, Max = p2 };
            Recalculate();
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
        }
    }
}