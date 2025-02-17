using SFML.Graphics;
using SFML.System;
using physics.Engine.Classes;
using System;
using System.Collections.Generic;

namespace physics.Engine.Shaders
{
    public class SFMLBoxShader : SFMLShader
    {
        private static readonly RectangleShape Rectangle = new RectangleShape();
        private static readonly RectangleShape Rectangle2 = new RectangleShape();
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
            // Calculate the size of the rectangle from the AABB.
            Vector2f size = new Vector2f(obj.Aabb.Max.X - obj.Aabb.Min.X, obj.Aabb.Max.Y - obj.Aabb.Min.Y);
            Rectangle.Size = size;
            // Set the origin to the center so that rotations are around the center.
            Rectangle.Origin = new Vector2f(size.X / 2, size.Y / 2);
            // Set the position to the object's center (its center of mass).
            Rectangle.Position = obj.Center;
            Rectangle.Rotation = obj.Angle * 180 / (float)Math.PI;
            Rectangle.FillColor = GrayColor;
            Rectangle.OutlineColor = RedColor;
            Rectangle.OutlineThickness = 1;

            target.Draw(Rectangle);

            //// DEBUG FOR OG Bounding box
            //// Calculate the size of the rectangle from the AABB.
            //Rectangle2.Size = new Vector2f(obj.Aabb.Max.X - obj.Aabb.Min.X, obj.Aabb.Max.Y - obj.Aabb.Min.Y);
            //Rectangle2.Position = obj.Aabb.Min;
            //Rectangle2.FillColor = Color.Transparent;
            //Rectangle2.OutlineColor = Color.Magenta;
            //Rectangle2.OutlineThickness = 1;

            //target.Draw(Rectangle2);
        }

    }
}
