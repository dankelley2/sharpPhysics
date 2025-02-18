using SFML.Graphics;
using SFML.System;
using physics.Engine.Objects;
using physics.Engine.Shapes;
using System;
using CircleShape = SFML.Graphics.CircleShape;

namespace physics.Engine.Shaders
{
    public class SFMLBallShader : SFMLShader
    {
        private static readonly CircleShape Circle = new CircleShape();
        private static readonly Color LightGrayColor = new Color(211, 211, 211);
        private static readonly Color ShadowColor = new Color(50, 50, 50, 100);

        public override void PreDraw(PhysicsObject obj, RenderTarget target)
        {
            if (!(obj.Shape is CirclePhysShape c))
            {
                throw new ArgumentException("GetRectangleCorners requires a PhysicsObject with a BoxShape.");
            }
            Circle.Radius = c.Radius;
            Circle.Position = new Vector2f(obj.Aabb.Min.X + 3, obj.Aabb.Min.Y + 5);
            Circle.FillColor = ShadowColor;
            target.Draw(Circle);
        }

        public override void Draw(PhysicsObject obj, RenderTarget target)
        {
            if (!(obj.Shape is CirclePhysShape c))
            {
                throw new ArgumentException("GetRectangleCorners requires a PhysicsObject with a BoxShape.");
            }
            Circle.Position = obj.Aabb.Min;
            Circle.FillColor = LightGrayColor;
            target.Draw(Circle);

            // Draw highlight
            Circle.Radius = c.Radius * 0.3f;
            Circle.Position = new Vector2f(
                obj.Aabb.Min.X + c.Radius * 0.2f,
                obj.Aabb.Min.Y + c.Radius * 0.2f);
            Circle.FillColor = Color.White;
            target.Draw(Circle);
        }

        public override void PostDraw(PhysicsObject obj, RenderTarget target)
        {
        }
    }
}
