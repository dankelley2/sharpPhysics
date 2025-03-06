using System.Numerics;
using SFML.System;

namespace physics.Engine.Helpers
{
    public static class SFMLExtensions
    {
        public static Vector2f ToSfml(this Vector2 vector)
            => new Vector2f(vector.X, vector.Y);

        public static Vector2 ToSystemNumerics(this Vector2f vector)
            => new Vector2(vector.X, vector.Y);
    }
}
