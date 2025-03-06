using System;
using System.Numerics;
using physics.Engine.Structs;

namespace physics.Engine.Helpers
{
    public static class PhysMath
    {


        // Helper: Returns a vector perpendicular to v (i.e., rotated 90 degrees)
        public static Vector2 Perpendicular(Vector2 v)
        {
            return new Vector2(-v.Y, v.X);
        }

        // Helper: Cross product in 2D (returns a scalar)
        // For vectors a and b, Cross(a, b) = a.X * b.Y - a.Y * b.X
        public static float Cross(Vector2 a, Vector2 b)
        {
            return a.X * b.Y - a.Y * b.X;
        }


        public static float Clamp(float low, float high, float val)
        {
            return Math.Max(low, Math.Min(val, high));
        }

        public static void CorrectBoundingBox(ref AABB aabb)
        {
            Vector2 p1 = new Vector2(Math.Min(aabb.Min.X, aabb.Max.X), Math.Min(aabb.Min.Y, aabb.Max.Y));
            Vector2 p2 = new Vector2(Math.Max(aabb.Min.X, aabb.Max.X), Math.Max(aabb.Min.Y, aabb.Max.Y));
            aabb.Min = new Vector2 { X = p1.X, Y = p1.Y };
            aabb.Max = new Vector2 { X = p2.X, Y = p2.Y };
        }

        public static void CorrectBoundingPoints(ref Vector2 p1, ref Vector2 p2)
        {
            Vector2 new_p1 = new Vector2(Math.Min(p1.X, p2.X), Math.Min(p1.Y, p2.Y));
            Vector2 new_p2 = new Vector2(Math.Max(p1.X, p2.X), Math.Max(p1.Y, p2.Y));

            p1 = new_p1;
            p2 = new_p2;
        }

        public static void RoundToZero(ref Vector2 vector, float cutoff)
        {
            if (vector.Length() < cutoff)
            {
                vector = Vector2.Zero;
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
        public static Vector2 RotateVector(Vector2 v, float angle)
        {
            float cos = (float)Math.Cos(angle);
            float sin = (float)Math.Sin(angle);
            return new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
        }

    }
}
