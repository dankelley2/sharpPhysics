using SFML.Graphics;
using SFML.System;
using physics.Engine.Classes;
using physics.Engine.Helpers;
using physics.Engine.Extensions;
using System;
using physics.Engine.Objects;
using physics.Engine.Shapes;

namespace physics.Engine.Shaders
{
    public class SFMLBallVelocityShader : SFMLShader
    {
        // Instance fields for drawing the ball.
        private readonly CircleShape _circle = new CircleShape();
        private readonly RectangleShape _velocityLine = new RectangleShape();
        private readonly RectangleShape _anglularTracker = new RectangleShape();

        public SFMLBallVelocityShader(int radius)
        {
            _circle.Radius = radius;
        }

        public override void PreDraw(PhysicsObject obj, RenderTarget target)
        {
            // (Optional) Any pre-draw setup can go here.
        }

        public override void Draw(PhysicsObject obj, RenderTarget target)
        {
            // Update ball's position.
            _circle.Position = obj.Aabb.Min;

            // Color based on velocity.
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

            // Draw the angular tracker.
            _anglularTracker.Position = new Vector2f(obj.Center.X, obj.Center.Y);
            _anglularTracker.Size = new Vector2f(_circle.Radius, 1);
            _anglularTracker.Rotation = obj.Angle * 180 / (float)Math.PI;
            _anglularTracker.FillColor = Color.White;
            target.Draw(_anglularTracker);

            // Draw a small circle at the object's last contact point.
            //CircleShape contactCircle = new CircleShape(3); // radius 3
            //contactCircle.FillColor = _contactPointColor;
            //contactCircle.Position = new Vector2f(obj.LastContactPoint.X - 3, obj.LastContactPoint.Y - 3);
            //target.Draw(contactCircle);
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
