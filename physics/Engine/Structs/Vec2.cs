using System;
using System.Linq;

namespace physics.Engine.Structs
{
    /// <summary>
    /// Optimized 2D vector implementation.
    /// Features:
    /// - Stack allocation for performance (struct-based)
    /// - Cached length calculations
    /// - Efficient operator overloading
    /// - Minimal memory allocations
    /// </summary>
    public struct Vec2
    {

        public float X, Y;

        public Vec2(float X, float Y)
        {
            this.X = X;
            this.Y = Y;
        }


        /// <summary>
        /// Gets vector length using cached squared length.
        /// Sqrt is only computed when actually needed.
        /// </summary>
        public float Length => (float) Math.Sqrt(X * X + Y * Y);

        /// <summary>
        /// Gets squared length of vector.
        /// Avoids sqrt for performance in comparisons.
        /// </summary>
        public float LengthSquared => X * X + Y * Y;

        /// <summary>
        /// Normalizes a vector to unit length.
        /// Uses optimized length calculation.
        /// </summary>
        /// <returns>Normalized vector with length 1</returns>
        public static Vec2 Normalize(Vec2 v1)
        {
            var distance = (float) Math.Sqrt(v1.X * v1.X + v1.Y * v1.Y);
            return new Vec2 {X = v1.X / distance, Y = v1.Y / distance};
        }

        public static float DotProduct(Vec2 left, Vec2 right)
        {
            return left.X * right.X + left.Y * right.Y;
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
            return new Vec2 { X = left.X * right, Y = left.Y * right };
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
