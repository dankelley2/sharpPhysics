using System.Numerics;
using System.Drawing;
using SFML.System;

namespace physics.Engine.Helpers
{
    public static class SFMLExtensions
    {
        public static Vector2 ToVector2(this PointF point)
            => new Vector2(point.X, point.Y);

        public static PointF ToPointF(this Vector2 vector)
            => new PointF(vector.X, vector.Y);

        public static Vector2f ToSfml(this Vector2 vector)
            => new Vector2f(vector.X, vector.Y);

        public static Vector2 ToSn(this Vector2f vector)
            => new Vector2(vector.X, vector.Y);
    }
}
