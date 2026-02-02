using SFML.Graphics;
using System;
using SharpPhysics.Engine.Objects;
using SharpPhysics.Engine.Shapes;
using SFML.System;
using SharpPhysics.Engine.Helpers;

namespace SharpPhysics.Rendering.Shaders
{
    public class SFMLBoxShader : SFMLShader
    {
        private static readonly RectangleShape Rectangle = new RectangleShape();
        private static readonly Color GrayColor = new Color(128, 128, 128);
        private static readonly Color RedColor = new Color(255, 50, 50);
        // Color for the contact point marker.

        public override void PreDraw(PhysicsObject obj, RenderTarget target)
        {
        }

        public override void Draw(PhysicsObject obj, RenderTarget target)
        {
        }

        public override void PostDraw(PhysicsObject obj, RenderTarget target)
        {
            if (!(obj.Shape is BoxPhysShape b))
            {
                throw new ArgumentException("GetRectangleCorners requires a PhysicsObject with a BoxShape.");
            }
            // Calculate the size of the rectangle from the AABB.
            Vector2f size = new Vector2f(b.Width, b.Height);
            Rectangle.Size = size;
            // Set the origin to the center so that rotations are around the center.
            Rectangle.Origin = new Vector2f(size.X / 2, size.Y / 2);
            // Set the position to the object's center (its center of mass).
            Rectangle.Position = obj.Center.ToSfml();
            Rectangle.Rotation = obj.Angle * 180 / (float)Math.PI;
            Rectangle.FillColor = GrayColor;
            Rectangle.OutlineColor = RedColor;
            Rectangle.OutlineThickness = 1;

            target.Draw(Rectangle);
        }

    }
}
