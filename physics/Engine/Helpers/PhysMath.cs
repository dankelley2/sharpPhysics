using System;
using System.Collections.Generic;
using System.Linq;
using SFML.System;
using physics.Engine.Classes;
using physics.Engine.Structs;
using physics.Engine.Extensions;

namespace physics.Engine.Helpers
{
    public static class PhysMath
    {


        // Helper: Returns a vector perpendicular to v (i.e., rotated 90 degrees)
        public static Vector2f Perpendicular(Vector2f v)
        {
            return new Vector2f(-v.Y, v.X);
        }

        // Helper: Cross product in 2D (returns a scalar)
        // For vectors a and b, Cross(a, b) = a.X * b.Y - a.Y * b.X
        public static float Cross(Vector2f a, Vector2f b)
        {
            return a.X * b.Y - a.Y * b.X;
        }


        public static float Clamp(float low, float high, float val)
        {
            return Math.Max(low, Math.Min(val, high));
        }

        // Helper: Dot product.
        public static float Dot(Vector2f a, Vector2f b)
        {
            return a.X * b.X + a.Y * b.Y;
        }

        public static decimal DotProduct(Vector2f pa, Vector2f pb)
        {
            decimal[] a = { (decimal)pa.X, (decimal)pa.Y };
            decimal[] b = { (decimal)pb.X, (decimal)pb.Y };
            return a.Zip(b, (x, y) => x * y).Sum();
        }

        public static void CorrectBoundingBox(ref AABB aabb)
        {
            Vector2f p1 = new Vector2f(Math.Min(aabb.Min.X, aabb.Max.X), Math.Min(aabb.Min.Y, aabb.Max.Y));
            Vector2f p2 = new Vector2f(Math.Max(aabb.Min.X, aabb.Max.X), Math.Max(aabb.Min.Y, aabb.Max.Y));
            aabb.Min = new Vector2f { X = p1.X, Y = p1.Y };
            aabb.Max = new Vector2f { X = p2.X, Y = p2.Y };
        }

        public static void CorrectBoundingPoints(ref Vector2f p1, ref Vector2f p2)
        {
            Vector2f new_p1 = new Vector2f(Math.Min(p1.X, p2.X), Math.Min(p1.Y, p2.Y));
            Vector2f new_p2 = new Vector2f(Math.Max(p1.X, p2.X), Math.Max(p1.Y, p2.Y));

            p1 = new_p1;
            p2 = new_p2;
        }

        public static void Clamp(ref Vector2f vector, Vector2f min, Vector2f max)
        {
            vector.X = Math.Max(min.X, Math.Min(max.X, vector.X));
            vector.Y = Math.Max(min.Y, Math.Min(max.Y, vector.Y));
        }

        public static void RoundToZero(ref Vector2f vector, float cutoff)
        {
            if (vector.Length() < cutoff)
            {
                vector.X = 0;
                vector.Y = 0;
            }
        }

        public static float RadiansToDegrees(this float rads)
        {
            return (float)(180 / Math.PI) * rads;
        }

        /// <summary>
        /// Rotates a vector by a given angle (in radians).
        /// </summary>
        /// <param name="v">The vector to rotate.</param>
        /// <param name="angle">The rotation angle in radians.</param>
        /// <returns>The rotated vector.</returns>
        public static Vector2f RotateVector(Vector2f v, float angle)
        {
            float cos = (float)Math.Cos(angle);
            float sin = (float)Math.Sin(angle);
            return new Vector2f(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
        }

    }
}
