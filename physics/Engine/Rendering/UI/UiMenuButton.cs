using System;
using System.Numerics;
using SFML.Graphics;
using SFML.System;

namespace physics.Engine.Rendering.UI
{
    /// <summary>
    /// A large, styled menu button for game selection screens.
    /// </summary>
    public class UiMenuButton : UiElement, IUiClickable
    {
        private RectangleShape _background;
        private RectangleShape _border;
        private Text _titleText;
        private Text _descriptionText;
        private Text _iconText;
        private Color _baseColor;
        private Color _hoverColor;
        private Color _borderColor;
        private bool _isHovered;

        public event Action<bool>? OnClick;

        public string Title
        {
            get => _titleText.DisplayedString;
            set => _titleText.DisplayedString = value;
        }

        public string Description
        {
            get => _descriptionText.DisplayedString;
            set => _descriptionText.DisplayedString = value;
        }

        public string Icon
        {
            get => _iconText.DisplayedString;
            set => _iconText.DisplayedString = value;
        }

        public UiMenuButton(
            string title,
            string description,
            Font font,
            Vector2 position,
            Vector2 size,
            string icon = "",
            Color? baseColor = null,
            Color? hoverColor = null,
            Color? borderColor = null)
        {
            Position = position;

            _baseColor = baseColor ?? new Color(60, 60, 80);
            _hoverColor = hoverColor ?? new Color(80, 80, 120);
            _borderColor = borderColor ?? new Color(100, 150, 255);

            // Main background
            _background = new RectangleShape(new Vector2f(size.X, size.Y))
            {
                FillColor = _baseColor,
                OutlineColor = new Color(80, 80, 100),
                OutlineThickness = 2
            };

            // Accent border (left side)
            _border = new RectangleShape(new Vector2f(6, size.Y))
            {
                FillColor = _borderColor
            };

            // Icon text (emoji)
            _iconText = new Text(icon, font)
            {
                FillColor = Color.White,
                CharacterSize = 48
            };

            // Title text
            _titleText = new Text(title, font)
            {
                FillColor = Color.White,
                CharacterSize = 28
            };

            // Description text
            _descriptionText = new Text(description, font)
            {
                FillColor = new Color(180, 180, 200),
                CharacterSize = 16
            };
        }

        public void SetHovered(bool hovered)
        {
            _isHovered = hovered;
            _background.FillColor = _isHovered ? _hoverColor : _baseColor;
            _background.OutlineColor = _isHovered ? _borderColor : new Color(80, 80, 100);
            _background.OutlineThickness = _isHovered ? 3 : 2;
        }

        protected override void DrawSelf(RenderTarget target)
        {
            float x = Position.X;
            float y = Position.Y;

            // Draw background
            _background.Position = new Vector2f(x, y);
            target.Draw(_background);

            // Draw accent border
            _border.Position = new Vector2f(x, y);
            target.Draw(_border);

            // Draw icon
            _iconText.Position = new Vector2f(x + 20, y + 15);
            target.Draw(_iconText);

            // Draw title
            _titleText.Position = new Vector2f(x + 90, y + 18);
            target.Draw(_titleText);

            // Draw description
            _descriptionText.Position = new Vector2f(x + 90, y + 55);
            target.Draw(_descriptionText);
        }

        bool IUiClickable.HandleClick(Vector2 clickPos)
        {
            var bounds = _background.GetGlobalBounds();
            if (clickPos.X >= bounds.Left && clickPos.X <= bounds.Left + bounds.Width &&
                clickPos.Y >= bounds.Top && clickPos.Y <= bounds.Top + bounds.Height)
            {
                OnClick?.Invoke(true);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Checks if the given position is within this button's bounds.
        /// </summary>
        public bool ContainsPoint(Vector2 point)
        {
            var bounds = _background.GetGlobalBounds();
            return point.X >= bounds.Left && point.X <= bounds.Left + bounds.Width &&
                   point.Y >= bounds.Top && point.Y <= bounds.Top + bounds.Height;
        }
    }
}
