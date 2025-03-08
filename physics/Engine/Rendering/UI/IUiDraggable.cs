using System.Numerics;

namespace physics.Engine.Rendering.UI
{
    public interface IUiDraggable
    {
        bool HandleDrag(Vector2 dragPos);
        void StopDrag();
    }
}
