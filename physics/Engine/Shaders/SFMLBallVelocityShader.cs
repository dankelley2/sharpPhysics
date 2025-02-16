using SFML.Graphics;
using SFML.System;
using physics.Engine.Classes;
using physics.Engine.Helpers;
using physics.Engine.Extensions;

namespace physics.Engine.Shaders
{
    public class SFMLBallVelocityShader : SFMLShader
    {
        private static readonly CircleShape Circle = new CircleShape();
        private static readonly RectangleShape VelocityLine = new RectangleShape();

        public override void PreDraw(PhysicsObject obj, RenderTarget target)
        {
        }

        public override void Draw(PhysicsObject obj, RenderTarget target)
        {
            Circle.Radius = obj.Width / 2;
            Circle.Position = new Vector2f(obj.Center.X - obj.Width / 2, obj.Center.Y - obj.Height / 2);

            // Color based on velocity
            double particleSpeed = 220 - System.Math.Min((int)obj.Velocity.Length(), 220);
            //ColorUtil.HsvToRgb(particleSpeed, 1, 1, out int r, out int g, out int b);

            byte r = 255;
            byte g = 255;
            byte b = 255;
            Circle.FillColor = new Color((byte)r, (byte)g, (byte)b);
            target.Draw(Circle);

            // Draw velocity line
            float velocityLength = System.Math.Min(obj.Velocity.Length() / 10, 50);
            if (velocityLength > 0)
            {
                float angle = (float)System.Math.Atan2(obj.Velocity.Y, obj.Velocity.X);
                VelocityLine.Size = new Vector2f(velocityLength, 2);
                VelocityLine.Position = new Vector2f(obj.Center.X, obj.Center.Y);
                VelocityLine.Rotation = angle * 180 / (float)System.Math.PI;
                VelocityLine.FillColor = Circle.FillColor;
                target.Draw(VelocityLine);
            }
        }

        public override void PostDraw(PhysicsObject obj, RenderTarget target)
        {
        }
    }
}
