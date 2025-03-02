using physics.Engine.Rendering.UI;
using SFML.Graphics;
using SFML.System;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace physics.Engine.Rendering.UI
{
    public abstract class UiElement
    {
        protected List<UiElement> Children { get; } = new List<UiElement>();
        protected UiElement Parent { get; private set; } = null;
        public Vector2f Position { get; set; } = new Vector2f(0, 0);

        public void Draw(RenderTarget target)
        {
            DrawSelf(target);
            foreach (var child in Children)
            {
                // Calculate absolute position based on parent
                Vector2f originalPosition = child.Position;
                child.Position = new Vector2f(Position.X + originalPosition.X, Position.Y + originalPosition.Y);
                child.Draw(target);
                // Restore original relative position
                child.Position = originalPosition;
            }
        }

        public void AddChild(UiElement child)
        {
            if (child != null)
            {
                child.Parent = this;
                Children.Add(child);
            }
        }

        public void RemoveChild(UiElement child)
        {
            if (child != null && Children.Contains(child))
            {
                child.Parent = null;
                Children.Remove(child);
            }
        }

        protected abstract void DrawSelf(RenderTarget target);
    }
}
