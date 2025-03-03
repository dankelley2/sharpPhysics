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

        public UiTextLabel(string text, Font font)
        {
            Text = text;
            Font = font;
            _textDrawable = new Text(Text, Font, CharacterSize)
            {
                FillColor = TextColor
            };
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