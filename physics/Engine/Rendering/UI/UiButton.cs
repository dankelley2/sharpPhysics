
using System;
using System.Numerics;
using SFML.Graphics;
using SFML.System;

namespace physics.Engine.Rendering.UI
{
    public class UiButton : UiElement, IUiClickable
    {
        private RectangleShape _background;
        private Text _buttonText;
        private bool _isPressed = false;
        public event Action<bool> OnClick;
        public string Text 
        { 
            get => _buttonText.DisplayedString; 
            set => _buttonText.DisplayedString = value;
        }

        public UiButton(string text, Font font, Vector2 position, Vector2 size)
        {
            Position = position;
            
            _background = new RectangleShape(new Vector2f((float)size.X, (float)size.Y))
            {
                FillColor = new Color(80, 80, 80),
                OutlineColor = new Color(120, 120, 120),
                OutlineThickness = 2
            };
            
            _buttonText = new Text(text, font)
            {
                FillColor = Color.White,
                CharacterSize = 16
            };
            
            // Center text on button
            CenterText();
        }

        private void CenterText()
        {
            var textBounds = _buttonText.GetGlobalBounds();
            var bgBounds = _background.GetGlobalBounds();
            
            _buttonText.Position = new Vector2f(
                _background.Position.X + (bgBounds.Width - textBounds.Width) / 2,
                _background.Position.Y + (bgBounds.Height - textBounds.Height) / 2 - 5 // Small adjustment for visual alignment
            );
        }
        
        protected override void DrawSelf(RenderTarget target)
        {
            _background.Position = new Vector2f((float)Position.X, (float)Position.Y);
            CenterText();
            
            target.Draw(_background);
            target.Draw(_buttonText);
        }

        bool IUiClickable.HandleClick(Vector2 clickPos)
        {
            var bounds = _background.GetGlobalBounds();
            if (clickPos.X >= bounds.Left && clickPos.X <= bounds.Left + bounds.Width &&
                clickPos.Y >= bounds.Top && clickPos.Y <= bounds.Top + bounds.Height)
            {
                _isPressed = true;
                _background.FillColor = new Color(100, 100, 100);
                OnClick?.Invoke(true);
                return true;
            }
            return false;
        }
    }
}
