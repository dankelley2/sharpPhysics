using SFML.Graphics;
using SFML.System;
using physics.Engine.Classes;

namespace physics.Engine.Shaders
{
    public class SFMLBallShader : SFMLShader
    {
        private static readonly CircleShape Circle = new CircleShape();
        private static readonly Color LightGrayColor = new Color(211, 211, 211);
        private static readonly Color ShadowColor = new Color(50, 50, 50, 100);

        public override void PreDraw(PhysicsObject obj, RenderTarget target)
        {
            Circle.Radius = obj.Width / 2;
            Circle.Position = new Vector2f(obj.Center.X - obj.Width / 2 + 3, obj.Center.Y - obj.Height / 2 + 5);
            Circle.FillColor = ShadowColor;
            target.Draw(Circle);
        }

        public override void Draw(PhysicsObject obj, RenderTarget target)
        {
            Circle.Position = new Vector2f(obj.Center.X - obj.Width / 2, obj.Center.Y - obj.Height / 2);
            Circle.FillColor = LightGrayColor;
            target.Draw(Circle);

            // Draw highlight
            Circle.Radius = obj.Width * 0.3f;
            Circle.Position = new Vector2f(
                obj.Center.X - Circle.Radius + obj.Width * 0.1f,
                obj.Center.Y - Circle.Radius + obj.Height * 0.1f);
            Circle.FillColor = Color.White;
            target.Draw(Circle);
        }

        public override void PostDraw(PhysicsObject obj, RenderTarget target)
        {
        }
    }
}
