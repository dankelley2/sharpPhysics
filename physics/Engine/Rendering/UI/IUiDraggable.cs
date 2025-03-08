using System;
using System.Numerics;

namespace physics.Engine.Rendering.UI
{
    public interface IUiDraggable
    {
        // Add drag-related events
        event Action<Vector2> OnDragStart;
        event Action<Vector2> OnDrag;
        event Action OnDragEnd;
        
        bool HandleDrag(Vector2 dragPos);
        void StopDrag();
    }
}
