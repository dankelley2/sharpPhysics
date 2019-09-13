using System;
using System.Linq;

namespace physics.Engine.Structs
{
    public struct Vec2
    {

        public float X, Y;

        public Vec2(float X, float Y)
        {
            this.X = X;
            this.Y = Y;
        }


        public float Length => (float) Math.Sqrt(X * X + Y * Y);

        public float LengthSquared => X * X + Y * Y;

        public static Vec2 Normalize(Vec2 v1)
        {
            var distance = (float) Math.Sqrt(v1.X * v1.X + v1.Y * v1.Y);
            return new Vec2 {X = v1.X / distance, Y = v1.Y / distance};
        }

        public static float DotProduct(Vec2 left, Vec2 right)
        {
            float[] a = {left.X, left.Y};
            float[] b = {right.X, right.Y};
            return a.Zip(b, (x, y) => x * y).Sum();
        }

        public static bool operator ==(Vec2 left, Vec2 right)
        {
            return left.X == right.X && left.Y == right.Y;
        }

        public static bool operator !=(Vec2 left, Vec2 right)
        {
            return left.X != right.X || left.Y != right.Y;
        }

        public static Vec2 operator +(Vec2 left, Vec2 right)
        {
            return new Vec2 {X = left.X + right.X, Y = left.Y + right.Y};
        }

        public static Vec2 operator +(Vec2 left, float right)
        {
            return new Vec2 {X = left.X + right, Y = left.Y + right};
        }

        public static Vec2 operator -(Vec2 left, Vec2 right)
        {
            return new Vec2 {X = left.X - right.X, Y = left.Y - right.Y};
        }

        public static Vec2 operator -(Vec2 v1)
        {
            return new Vec2 {X = -v1.X, Y = -v1.Y};
        }

        public static Vec2 operator -(Vec2 left, float right)
        {
            return new Vec2 {X = left.X - right, Y = left.Y - right};
        }

        public static Vec2 operator *(Vec2 left, Vec2 right)
        {
            return new Vec2 {X = left.X * right.X, Y = left.Y * right.Y};
        }

        public static Vec2 operator *(Vec2 left, float right)
        {
            return new Vec2 {X = left.X * right, Y = left.Y * right};
        }

        public static Vec2 operator /(Vec2 left, Vec2 right)
        {
            return new Vec2 {X = left.X / right.X, Y = left.Y / right.Y};
        }

        public static Vec2 operator /(Vec2 left, float right)
        {
            return new Vec2 {X = left.X / right, Y = left.Y / right};
        }
    }
}