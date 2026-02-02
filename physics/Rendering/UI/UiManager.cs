using System.Collections.Generic;
using System.Numerics;
using SFML.Graphics;

namespace SharpPhysics.Rendering.UI
{
    /// <summary>
    /// Manages UI elements for a specific context (game, menu, etc.).
    /// Each game should create its own UiManager instance instead of using global state.
    /// </summary>
    public class UiManager
    {
        private readonly List<UiElement> _elements = new();
        private UiElement? _draggedElement;

        /// <summary>
        /// Gets the list of UI elements managed by this instance.
        /// </summary>
        public IReadOnlyList<UiElement> Elements => _elements;

        /// <summary>
        /// Gets the currently dragged element, if any.
        /// </summary>
        public UiElement? DraggedElement => _draggedElement;

        /// <summary>
        /// Adds a UI element to this manager.
        /// </summary>
        public void Add(UiElement element)
        {
            if (element != null && !_elements.Contains(element))
            {
                _elements.Add(element);
            }
        }

        /// <summary>
        /// Removes a UI element from this manager.
        /// </summary>
        public bool Remove(UiElement element)
        {
            return _elements.Remove(element);
        }

        /// <summary>
        /// Clears all UI elements from this manager.
        /// </summary>
        public void Clear()
        {
            _elements.Clear();
            _draggedElement = null;
        }

        /// <summary>
        /// Handles a click at the specified position.
        /// Returns true if a UI element handled the click.
        /// </summary>
        public bool HandleClick(Vector2 clickPos)
        {
            // Check elements in reverse order (top-most first)
            for (int i = _elements.Count - 1; i >= 0; i--)
            {
                var element = _elements[i];
                if (element.HandleClick(clickPos))
                {
                    // Set as dragged element if it's draggable
                    if (element is IUiDraggable)
                    {
                        _draggedElement = element;
                    }
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Handles drag events for the currently dragged element.
        /// </summary>
        public bool HandleDrag(Vector2 dragPos)
        {
            if (_draggedElement is IUiDraggable draggable)
            {
                return draggable.HandleDrag(dragPos);
            }
            return false;
        }

        /// <summary>
        /// Stops the current drag operation.
        /// </summary>
        public void StopDrag()
        {
            if (_draggedElement is IUiDraggable draggable)
            {
                draggable.StopDrag();
            }
            _draggedElement = null;
        }

        /// <summary>
        /// Draws all UI elements to the render target.
        /// </summary>
        public void Draw(RenderTarget target)
        {
            foreach (var element in _elements)
            {
                element.Draw(target);
            }
        }
    }
}
