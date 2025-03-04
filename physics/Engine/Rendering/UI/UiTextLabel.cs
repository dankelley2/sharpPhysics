using SFML.Graphics;
using SFML.System;
using System;

namespace physics.Engine.Rendering.UI
{
    public class UiTextLabel : UiElement
    {
        public string Text { get; set; }
        public Font Font { get; set; }
        public uint CharacterSize { get; set; } = 12;
        public Color TextColor { get; set; } = Color.White;

        private Text _textDrawable;

using System;

namespace physics.Engine.Rendering.UI
{
    public class UiTextLabel : UiElement, IDisposable
    {
        // Properties...
        
        private Text _textDrawable;
        private bool _disposed = false;

        public UiTextLabel(string text, Font font)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));
            if (font == null)
                throw new ArgumentNullException(nameof(font));
                
            Text = text;
            Font = font;
            _textDrawable = new Text(Text, Font, CharacterSize)
            {
                FillColor = TextColor
            };
        }
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _textDrawable?.Dispose();
                }
                _disposed = true;
            }
        }
    }
}

        protected override void DrawSelf(RenderTarget target)
        {
            _textDrawable.DisplayedString = Text;
            _textDrawable.Font = Font;
            _textDrawable.CharacterSize = CharacterSize;
            _textDrawable.FillColor = TextColor;
            _textDrawable.Position = Position;

            target.Draw(_textDrawable);
        }
    }
}