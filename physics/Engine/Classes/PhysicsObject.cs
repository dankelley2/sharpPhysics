using System;
using System.Runtime.CompilerServices;
using SFML.System;
using physics.Engine.Helpers;
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

        public SFMLShader Shader;
        public bool Locked;
        public AABB Aabb;
        public Vector2f Velocity;
        public Vector2f Center;
        public Vector2f Pos;
        public float Width;
        public float Height;
        public float Restitution;
        public float Mass;
        public float IMass;

        public PhysicsObject(AABB boundingBox, Type t, float r, bool locked, SFMLShader shader, float m = 0)
        {
            Velocity = new Vector2f(0, 0);
            Aabb = boundingBox;
            Width = Aabb.Max.X - Aabb.Min.X;
            Height = Aabb.Max.Y - Aabb.Min.Y;
            Pos = new Vector2f(Aabb.Min.X, Aabb.Min.Y);
            Center = new Vector2f(Pos.X + Width / 2, Pos.Y + Height / 2);
            ShapeType = t;
            Restitution = r;
            Mass = (int)m == 0 ? Aabb.Area : m;
            IMass = 1 / Mass;
            Locked = locked;
            Shader = shader;
        }

        public Type ShapeType { get; set; }
        public Manifold LastCollision { get; internal set; }

        public bool Contains(Vector2f p)
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

        public void Move(float dt)
        {
            if (Mass >= 1000000)
            {
                return;
            }
            RoundSpeedToZero();

            var p1 = Aabb.Min + (Velocity * dt);
            var p2 = Aabb.Max + (Velocity * dt);
            Aabb = new AABB { Min = p1, Max = p2 };
            Recalculate();
        }

        private void RoundSpeedToZero()
        {
            if (Math.Abs(this.Velocity.X) + Math.Abs(this.Velocity.Y) < .01F)
            {
                Velocity.X = 0;
                Velocity.Y = 0;
            }
        }

        private void Recalculate()
        {
            Width = Aabb.Max.X - Aabb.Min.X;
            Height = Aabb.Max.Y - Aabb.Min.Y;
            Pos.X = Aabb.Min.X;
            Pos.Y = Aabb.Min.Y;
            Center.X = Pos.X + Width / 2;
            Center.Y = Pos.Y + Height / 2;
        }

        public void Move(Vector2f dVector)
        {
            if (Locked)
            {
                return;
            }

            Aabb.Min = Aabb.Min + dVector;
            Aabb.Max = Aabb.Max + dVector;
            Recalculate();
        }
    }
}
