using SharpPhysics.Engine.Helpers;
using SFML.Graphics;
using System;
using System.Numerics;

namespace SharpPhysics.Rendering.UI
{
    public class UiSlider : UiElement, IUiClickable, IUiDraggable
    {
        public float MinValue { get; set; }
        public float MaxValue { get; set; }
        public float Value { get; private set; }
        
        public Vector2 Size { get; set; }
        private bool isDragging = false;
        
        // Event for when the slider value changes
        public event Action<float>? OnValueChanged;
        
        // Implement IUiClickable event
        public event Action<bool>? OnClick;
        
        // Implement IUiDraggable events
        public event Action<Vector2>? OnDragStart;
        public event Action<Vector2>? OnDrag;
        public event Action? OnDragEnd;
        
        public UiSlider(Vector2 position, Vector2 size, float minValue = 0f, float maxValue = 1f, float initialValue = 0f)
        {
            this.Position = position;
            this.Size = size;
            this.MinValue = minValue;
            this.MaxValue = maxValue;
            SetValue(initialValue);
        }
        
        public void SetValue(float value)
        {
            value = Math.Clamp(value, MinValue, MaxValue);
            if (Value != value)
            {
                Value = value;
                OnValueChanged?.Invoke(Value);
            }
        }
        
        // Calculate the normalized position (0.0 to 1.0) of the slider handle
        private float GetNormalizedPosition()
        {
            return (Value - MinValue) / (MaxValue - MinValue);
        }
        
        // Set the value based on a normalized position (0.0 to 1.0)
        private void SetNormalizedPosition(float position)
        {
            position = Math.Clamp(position, 0f, 1f);
            SetValue(MinValue + position * (MaxValue - MinValue));
        }
        
        bool IUiClickable.HandleClick(Vector2 clickPos)
        {
            if (clickPos.X >= Position.X && clickPos.X <= Position.X + Size.X &&
                clickPos.Y >= Position.Y && clickPos.Y <= Position.Y + Size.Y)
            {
                // Calculate the new value based on where the user clicked
                float normalizedPosition = (clickPos.X - Position.X) / Size.X;
                SetNormalizedPosition(normalizedPosition);
                
                // Start dragging
                isDragging = true;
                OnDragStart?.Invoke(clickPos);
                OnClick?.Invoke(true); // Firing click event
                
                return true;
            }
            return false;
        }
        
        // Implement IUiDraggable interface
        public bool HandleDrag(Vector2 dragPos)
        {
            if (isDragging)
            {
                float normalizedPosition = Math.Clamp((dragPos.X - Position.X) / Size.X, 0f, 1f);
                SetNormalizedPosition(normalizedPosition);
                OnDrag?.Invoke(dragPos);
                return true;
            }
            return false;
        }
        
        public void StopDrag()
        {
            if (isDragging)
            {
                isDragging = false;
                OnDragEnd?.Invoke();
                OnClick?.Invoke(false); // Release event
            }
        }
        
        protected override void DrawSelf(RenderTarget target)
        {
            // Draw the slider background
            var background = new RectangleShape(new SFML.System.Vector2f(Size.X, Size.Y))
            {
                Position = Position.ToSfml(),
                FillColor = new Color(200, 200, 200),
                OutlineThickness = 1,
                OutlineColor = Color.Black
            };
            target.Draw(background);
            
            // Draw the filled portion of the slider
            var filled = new RectangleShape(new SFML.System.Vector2f(Size.X * GetNormalizedPosition(), Size.Y))
            {
                Position = Position.ToSfml(),
                FillColor = new Color(100, 100, 200)
            };
            target.Draw(filled);
            
            // Draw the slider handle
            float handlePosition = GetNormalizedPosition() * Size.X;
            float handleWidth = 8f; // Width of the handle
            float handleHeight = Size.Y + 6f; // Height of the handle, slightly taller than the background
            
            var handle = new RectangleShape(new SFML.System.Vector2f(handleWidth, handleHeight))
            {
                Position = new SFML.System.Vector2f(Position.X + handlePosition - handleWidth/2, Position.Y - 3),
                FillColor = new Color(50, 50, 150),
                OutlineThickness = 1,
                OutlineColor = Color.Black
            };
            target.Draw(handle);
        }
    }
}
