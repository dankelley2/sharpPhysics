using System;
using System.Numerics;

namespace physics.Engine.Rendering.UI
{
    public interface IUiClickable
    {
        // Move the event to the interface
        event Action<bool>? OnClick;
        
        // Keep the click handling method
        bool HandleClick(Vector2 clickPos);
    }
}
