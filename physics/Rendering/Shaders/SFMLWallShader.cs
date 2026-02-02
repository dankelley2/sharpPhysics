using System;
using physics.Engine.Objects;
using SFML.Graphics;
using SFML.System;

namespace SharpPhysics.Rendering.Shaders
{
    public class SFMLWallShader : SFMLShader
    {
        private static readonly RectangleShape Rectangle = new RectangleShape();
        private static readonly Color GrayColor = new Color(128, 128, 128);

        public override void PreDraw(PhysicsObject obj, RenderTarget target)
        {
        }

        public override void Draw(PhysicsObject obj, RenderTarget target)
        {
        }

        public override void PostDraw(PhysicsObject obj, RenderTarget target)
        {
            var size = new Vector2f(obj.Aabb.Max.X - obj.Aabb.Min.X, obj.Aabb.Max.Y - obj.Aabb.Min.Y);
            Rectangle.Size = size;
            Rectangle.Origin = new Vector2f(size.X / 2f, size.Y / 2f);
            Rectangle.Position = new Vector2f(obj.Center.X, obj.Center.Y);
            Rectangle.Rotation = obj.Angle * 180f / MathF.PI;
            Rectangle.FillColor = GrayColor;
            target.Draw(Rectangle);
        }
    }
}
