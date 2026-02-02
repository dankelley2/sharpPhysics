using SFML.Graphics;
using System;
using physics.Engine.Objects;
using physics.Engine.Helpers;
using SFML.System;

namespace SharpPhysics.Rendering.Shaders
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
            _circle.Position = obj.Aabb.Min.ToSfml();

            // Color based on velocity.
            double particleSpeed = 220 - Math.Min((int)obj.Velocity.Length(), 220);
            double hue = particleSpeed % 360;
            if (obj.Sleeping)
            {
                _circle.FillColor = new Color(50,50,50);
            }
            else
            {
                ShaderHelpers.HsvToRgb(hue, 1, 1, out int red, out int green, out int blue);
                _circle.FillColor = new Color((byte)red, (byte)green, (byte)blue);
            }
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
        }

        public override void PostDraw(PhysicsObject obj, RenderTarget target)
        {
            // (Optional) Any post-draw cleanup can go here.
        }
    }
}
