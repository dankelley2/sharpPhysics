using SFML.Graphics;
using SFML.System;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace physics.Engine.Rendering.UI
{
    public abstract class aUiElement
    {
        private List<aUiElement> Children { get; set; } = new List<aUiElement>();

        private aUiElement Parent { get; set; } = null;

        public Vector2f Position { get; set; } = new Vector2f(0, 0);

        public void Draw(RenderTarget target)
        {
            DrawSelf(target);
            foreach (var child in Children)
            {
                child.Draw(target);
            }
        }

        protected abstract void DrawSelf(RenderTarget target);
    }
}
