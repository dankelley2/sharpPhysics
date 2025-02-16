using SFML.Graphics;
using SFML.System;
using physics.Engine.Classes;
using physics.Engine.Helpers;
using physics.Engine.Extensions;
using System;

namespace physics.Engine.Shaders
{
    public class SFMLBallVelocityShader : SFMLShader
    {
        // Use instance fields rather than static fields so that each shader instance can cache its state.
        private readonly CircleShape _circle = new CircleShape();
        private readonly RectangleShape _velocityLine = new RectangleShape();

        public override void PreDraw(PhysicsObject obj, RenderTarget target)
        {
            // (Optional) Any pre-draw setup can go here.
        }

        public override void Draw(PhysicsObject obj, RenderTarget target)
        {
            // Only update the circle’s radius if the physics object’s geometry changed.
            if (obj.GeometryChanged)
            {
                _circle.Radius = obj.Width / 2;
            }
            // Always update the circle’s position (since the center may move even if the size doesn’t change).
            _circle.Position = new Vector2f(obj.Center.X - obj.Width / 2, obj.Center.Y - obj.Height / 2);

            // Color based on velocity.
            // The particleSpeed value is computed and then treated as a hue (0 to 360).
            double particleSpeed = 220 - Math.Min((int)obj.Velocity.Length(), 220);
            double hue = particleSpeed % 360;
            HsvToRgb(hue, 1, 1, out int red, out int green, out int blue);
            _circle.FillColor = new Color((byte)red, (byte)green, (byte)blue);
            target.Draw(_circle);

            // Draw the velocity line.
            float velocityLength = Math.Min(obj.Velocity.Length() / 10, 50);
            if (velocityLength > 0)
            {
                float angle = (float)Math.Atan2(obj.Velocity.Y, obj.Velocity.X);
                _velocityLine.Size = new Vector2f(velocityLength, 2);
                _velocityLine.Position = new Vector2f(obj.Center.X, obj.Center.Y);
                _velocityLine.Rotation = angle * 180 / (float)Math.PI;
                _velocityLine.FillColor = _circle.FillColor;
                target.Draw(_velocityLine);
            }
        }

        public override void PostDraw(PhysicsObject obj, RenderTarget target)
        {
            // (Optional) Any post-draw cleanup can go here.
        }

        /// <summary>
        /// Converts an HSV color value to RGB.
        /// h (hue) should be in [0,360), s (saturation) and v (value) in [0,1].
        /// </summary>
        public static void HsvToRgb(double h, double s, double v, out int r, out int g, out int b)
        {
            double c = v * s;
            double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
            double m = v - c;
            double r1, g1, b1;

            if (h < 60)
            {
                r1 = c; g1 = x; b1 = 0;
            }
            else if (h < 120)
            {
                r1 = x; g1 = c; b1 = 0;
            }
            else if (h < 180)
            {
                r1 = 0; g1 = c; b1 = x;
            }
            else if (h < 240)
            {
                r1 = 0; g1 = x; b1 = c;
            }
            else if (h < 300)
            {
                r1 = x; g1 = 0; b1 = c;
            }
            else
            {
                r1 = c; g1 = 0; b1 = x;
            }

            r = (int)((r1 + m) * 255);
            g = (int)((g1 + m) * 255);
            b = (int)((b1 + m) * 255);
        }
    }
}
