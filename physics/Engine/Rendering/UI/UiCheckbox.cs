using physics.Engine.Helpers;
using SFML.Graphics;
using System;
using System.Numerics;

namespace physics.Engine.Rendering.UI
{
    public class UiCheckbox : UiElement, IUiClickable
    {
        public bool IsChecked { get; set; }
        public Vector2 Size { get; set; }
        
        // Implement the event from IUiClickable
        public event Action<bool>? OnClick;
        
        public UiCheckbox(Vector2 position, Vector2 size)
        {
            this.Position = position;
            this.Size = size;
        }

        bool IUiClickable.HandleClick(Vector2 clickPos)
        {
            if (clickPos.X >= Position.X && clickPos.X <= Position.X + Size.X &&
                clickPos.Y >= Position.Y && clickPos.Y <= Position.Y + Size.Y)
            {
                IsChecked = !IsChecked; // toggle state
                OnClick?.Invoke(IsChecked); // Fire the event directly
                return true;
            }
            return false;
        }

        protected override void DrawSelf(RenderTarget target)
        {
            var rect = new RectangleShape(new SFML.System.Vector2f(Size.X, Size.Y))
            {
                Position = Position.ToSfml(),
                FillColor = IsChecked ? new Color(100, 200, 100) : new Color(200, 200, 200),
                OutlineThickness = 2,
                OutlineColor = Color.Black
            };
            target.Draw(rect);
        }
    }
}
