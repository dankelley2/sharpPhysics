using physics.Engine.Rendering.UI;
using SFML.Graphics;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace physics.Engine.Rendering.UI
{
    public abstract class UiElement
    {
        protected List<UiElement> Children { get; } = new List<UiElement>();
        protected UiElement Parent { get; private set; } = null;
        public Vector2 Position { get; set; } = new Vector2(0, 0);

        public void Draw(RenderTarget target)
        {
            DrawSelf(target);
            foreach (var child in Children)
            {
                // Calculate absolute position based on parent
                Vector2 originalPosition = child.Position;
                child.Position = new Vector2(Position.X + originalPosition.X, Position.Y + originalPosition.Y);
                child.Draw(target);
                // Restore original relative position
                child.Position = originalPosition;
            }
        }

        public void AddChild(UiElement child)
        {
            if (child != null)
            {
                // Avoid self or cyclic references
                if (child == this || IsDescendantOf(child))
                {
                    throw new InvalidOperationException("Cannot add a UiElement as a child of itself or one of its descendants.");
                }

                // If the child already has a parent, remove it from that parent's children
                if (child.Parent != null && child.Parent != this)
                {
                    child.Parent.RemoveChild(child);
                }
                child.Parent = this;
                Children.Add(child);
            }
        }

        private bool IsDescendantOf(UiElement possibleAncestor)
        {
            UiElement current = this.Parent;
            while (current != null)
            {
                if (current == possibleAncestor) return true;
                current = current.Parent;
            }
            return false;
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
