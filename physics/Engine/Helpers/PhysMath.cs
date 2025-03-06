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

        public static void RoundToZero(ref Vector2 vector, float cutoff)
        {
            if (vector.LengthSquared() < cutoff * cutoff)
            {
                vector = Vector2.Zero;
            }
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
