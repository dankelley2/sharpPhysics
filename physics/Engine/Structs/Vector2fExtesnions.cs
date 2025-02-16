using SFML.System;
using System;
using System.Linq;
using System.Runtime.CompilerServices;

namespace physics.Engine.Extensions
{
    public static class Extensions
    {

        public static float Length(this Vector2f vector)
        {
            return (float)Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y);
        }
        public static float LengthSquared(this Vector2f vector)
        {
            return vector.X * vector.X + vector.Y * vector.Y;
        }

        public static Vector2f Normalize(this Vector2f v1)
        {
            var distance = (float)Math.Sqrt(v1.X * v1.X + v1.Y * v1.Y);
            return new Vector2f { X = v1.X / distance, Y = v1.Y / distance };
        }

        public static Vector2f Minus(this Vector2f left, float right)
        {
            return new Vector2f { X = left.X - right, Y = left.Y - right };
        }

        public static float DotProduct(Vector2f left, Vector2f right)
        {
            return left.X * right.X + left.Y * right.Y;
        }

        //public static bool operator ==(Vector2f left, Vector2f right)
        //{
        //    return left.X == right.X && left.Y == right.Y;
        //}

        //public static bool operator !=(Vector2f left, Vector2f right)
        //{
        //    return left.X != right.X || left.Y != right.Y;
        //}

        //public static Vector2f operator +(Vector2f left, Vector2f right)
        //{
        //    return new Vector2f { X = left.X + right.X, Y = left.Y + right.Y };
        //}

        //public static Vector2f operator +(Vector2f left, float right)
        //{
        //    return new Vector2f { X = left.X + right, Y = left.Y + right };
        //}

        //public static Vector2f operator -(Vector2f left, Vector2f right)
        //{
        //    return new Vector2f { X = left.X - right.X, Y = left.Y - right.Y };
        //}

        //public static Vector2f operator -(Vector2f v1)
        //{
        //    return new Vector2f { X = -v1.X, Y = -v1.Y };
        //}

        //public static Vector2f operator -(Vector2f left, float right)
        //{
        //    return new Vector2f { X = left.X - right, Y = left.Y - right };
        //}

        //public static Vector2f operator *(Vector2f left, Vector2f right)
        //{
        //    return new Vector2f { X = left.X * right.X, Y = left.Y * right.Y };
        //}

        //public static Vector2f operator *(Vector2f left, float right)
        //{
        //    return new Vector2f { X = left.X * right, Y = left.Y * right };
        //}

        //public static Vector2f operator /(Vector2f left, Vector2f right)
        //{
        //    return new Vector2f { X = left.X / right.X, Y = left.Y / right.Y };
        //}

        //public static Vector2f operator /(Vector2f left, float right)
        //{
        //    return new Vector2f { X = left.X / right, Y = left.Y / right };
        //}
    }
}