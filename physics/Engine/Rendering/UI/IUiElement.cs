using SFML.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace physics.Engine.Rendering.UI
{
    public abstract class aUiElement
    {
        List<aUiElement> Children { get; set; } = new List<aUiElement>();

        aUiElement Parent { get; set; } = null;

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
