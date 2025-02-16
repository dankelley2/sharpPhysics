using SFML.System;
using System.Drawing;

namespace physics.Engine.Helpers
{
    public static class SFMLExtensions
    {
        public static Vector2f ToVector2f(this PointF point)
            => new Vector2f(point.X, point.Y);

        public static PointF ToPointF(this Vector2f vector)
            => new PointF(vector.X, vector.Y);
    }
}
