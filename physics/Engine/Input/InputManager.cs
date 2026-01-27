using SFML.Graphics;
using SFML.System;
using SFML.Window;
using System.Collections.Generic;
using System.Numerics;
using physics.Engine.Helpers;

namespace physics.Engine.Input
{
    /// <summary>
    /// Flexible input manager that tracks any keyboard key or mouse button.
    /// Games query the input state directly rather than using a fixed KeyState struct.
    /// </summary>
    public class InputManager
    {
        private readonly RenderWindow _window;

        // Keyboard state - tracks ALL keys generically
        private readonly HashSet<Keyboard.Key> _heldKeys = new();
        private readonly HashSet<Keyboard.Key> _previousHeldKeys = new();
        private readonly Dictionary<Keyboard.Key, float> _keyPressBuffers = new();

        // Mouse button state - tracks ALL buttons generically
        private readonly HashSet<Mouse.Button> _heldMouseButtons = new();
        private readonly HashSet<Mouse.Button> _previousMouseButtons = new();

        // Reusable collections to avoid allocations during update
        private readonly List<Keyboard.Key> _expiredKeys = new();

        // Configuration
        private const float PRESS_BUFFER_TIME = 0.15f; // Keys stay "pressed" for 150ms

        // Scroll wheel delta (accumulated per frame, reset after EndFrame)
        private float _scrollWheelDelta = 0f;

        // View reference for coordinate transformation (set externally)
        private View? _currentView;

        #region Public Properties

        /// <summary>
        /// Mouse position in world coordinates (accounts for view panning/zoom).
        /// Use this for game logic, physics interactions, and drawing.
        /// </summary>
        public Vector2 MousePosition { get; private set; }

        /// <summary>
        /// Mouse position in screen/pixel coordinates (raw window position).
        /// Use this for UI hit detection and toolbar interactions.
        /// </summary>
        public Vector2 MouseScreenPosition { get; private set; }

        /// <summary>
        /// Scroll wheel delta this frame. Positive = scroll up, Negative = scroll down.
        /// </summary>
        public float ScrollWheelDelta => _scrollWheelDelta;

        #endregion

        #region Constructor

        public InputManager(RenderWindow window)
        {
            _window = window;

            // Subscribe to window events
            window.MouseButtonPressed += OnMouseButtonPressed;
            window.MouseButtonReleased += OnMouseButtonReleased;
            window.MouseMoved += OnMouseMoved;
            window.MouseWheelScrolled += OnMouseWheelScrolled;
            window.KeyPressed += OnKeyPressed;
            window.KeyReleased += OnKeyReleased;
        }

        #endregion

        #region Frame Lifecycle

        /// <summary>
        /// Sets the view used for mouse coordinate transformation.
        /// Call this each frame before Update() with the current game view.
        /// </summary>
        public void SetView(View view)
        {
            _currentView = view;
        }

        /// <summary>
        /// Called once per frame to decay input buffers.
        /// </summary>
        public void Update(float deltaTime)
        {
            // Decay key press buffers
            _expiredKeys.Clear();
            foreach (var kvp in _keyPressBuffers)
            {
                if (kvp.Value - deltaTime <= 0)
                {
                    _expiredKeys.Add(kvp.Key);
                }
            }

            foreach (var key in _expiredKeys)
            {
                _keyPressBuffers.Remove(key);
            }

            // Decay remaining timers (can't modify during enumeration, so use Keys snapshot)
            foreach (var key in new List<Keyboard.Key>(_keyPressBuffers.Keys))
            {
                _keyPressBuffers[key] -= deltaTime;
            }
        }

        /// <summary>
        /// Called at the end of each frame to update previous state for edge detection.
        /// Call this after all game logic has processed input.
        /// </summary>
        public void EndFrame()
        {
            // Store current state as previous for next frame's edge detection
            _previousHeldKeys.Clear();
            foreach (var key in _heldKeys)
            {
                _previousHeldKeys.Add(key);
            }

            _previousMouseButtons.Clear();
            foreach (var btn in _heldMouseButtons)
            {
                _previousMouseButtons.Add(btn);
            }

            // Reset scroll wheel delta
            _scrollWheelDelta = 0f;
        }

        #endregion

        #region Keyboard Query Methods

        /// <summary>
        /// Returns true if the key is currently held down.
        /// </summary>
        public bool IsKeyHeld(Keyboard.Key key) => _heldKeys.Contains(key);

        /// <summary>
        /// Returns true only on the frame the key was first pressed (edge detection).
        /// </summary>
        public bool IsKeyPressed(Keyboard.Key key) => _heldKeys.Contains(key) && !_previousHeldKeys.Contains(key);

        /// <summary>
        /// Returns true if the key was pressed within the buffer window (default 150ms).
        /// Use this for actions that benefit from input buffering (e.g., jump).
        /// </summary>
        public bool IsKeyPressedBuffered(Keyboard.Key key) => _keyPressBuffers.TryGetValue(key, out var time) && time > 0;

        /// <summary>
        /// Consumes a buffered key press so it won't trigger again.
        /// Call this after handling a buffered press to prevent double-triggering.
        /// </summary>
        public void ConsumeKeyPress(Keyboard.Key key) => _keyPressBuffers.Remove(key);

        #endregion

        #region Mouse Query Methods

        /// <summary>
        /// Returns true if the mouse button is currently held down.
        /// </summary>
        public bool IsMouseHeld(Mouse.Button button) => _heldMouseButtons.Contains(button);

        /// <summary>
        /// Returns true only on the frame the mouse button was first pressed (edge detection).
        /// </summary>
        public bool IsMousePressed(Mouse.Button button) => _heldMouseButtons.Contains(button) && !_previousMouseButtons.Contains(button);

        #endregion

        #region Event Handlers

        private void OnMouseButtonPressed(object? sender, MouseButtonEventArgs e)
        {
            _heldMouseButtons.Add(e.Button);

            // Update mouse position on click (in case mouse hasn't moved since last frame)
            MouseScreenPosition = new Vector2(e.X, e.Y);
            if (_currentView != null)
            {
                MousePosition = _window.MapPixelToCoords(new Vector2i(e.X, e.Y), _currentView).ToSystemNumerics();
            }
            else
            {
                MousePosition = MouseScreenPosition;
            }
        }

        private void OnMouseButtonReleased(object? sender, MouseButtonEventArgs e)
        {
            _heldMouseButtons.Remove(e.Button);
        }

        private void OnMouseMoved(object? sender, MouseMoveEventArgs e)
        {
            // Store screen (raw pixel) coordinates
            MouseScreenPosition = new Vector2(e.X, e.Y);

            // Transform to world coordinates using current view
            if (_currentView != null)
            {
                MousePosition = _window.MapPixelToCoords(new Vector2i(e.X, e.Y), _currentView).ToSystemNumerics();
            }
            else
            {
                MousePosition = MouseScreenPosition;
            }
        }

        private void OnMouseWheelScrolled(object? sender, MouseWheelScrollEventArgs e)
        {
            _scrollWheelDelta += e.Delta;
        }

        private void OnKeyPressed(object? sender, KeyEventArgs e)
        {
            // Only buffer if this is a fresh press (not auto-repeat)
            if (!_heldKeys.Contains(e.Code))
            {
                _keyPressBuffers[e.Code] = PRESS_BUFFER_TIME;
            }
            _heldKeys.Add(e.Code);
        }

        private void OnKeyReleased(object? sender, KeyEventArgs e)
        {
            _heldKeys.Remove(e.Code);
        }

        #endregion
    }
}