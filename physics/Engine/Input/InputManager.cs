using SFML.Graphics;
using SFML.System;
using SFML.Window;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using physics.Engine.Helpers;

namespace physics.Engine.Input
{
    public class InputManager
    {
        private RenderWindow window;
        private View view;

        // Input state properties - now minimal, just for panning
        public bool IsPanning { get; private set; } = false;
        public Vector2 MousePosition { get; private set; }
        public Vector2 PanStartPos { get; private set; }

        // Key state for polling important keys.
        private KeyState keyState = new KeyState();

        // Previous frame's key state for edge detection
        private KeyState prevKeyState = new KeyState();

        // Press time tracking for input buffering (keys stay "pressed" for a short duration)
        private Dictionary<string, float> _pressTimers = new Dictionary<string, float>();
        private readonly List<string> _keysToRemove = new List<string>(); // Reusable list for removal
        private const float PRESS_BUFFER_TIME = 0.15f; // Keys stay "pressed" for 150ms

        // Mouse button state tracking
        private bool _leftMouseDown = false;
        private bool _rightMouseDown = false;
        private bool _prevLeftMouseDown = false;
        private bool _prevRightMouseDown = false;

        public InputManager(RenderWindow window, PhysicsSystem physicsSystem, View view)
        {
            this.window = window;
            this.view = view;

            // Subscribe to window events.
            window.MouseButtonPressed += OnMouseButtonPressed;
            window.MouseButtonReleased += OnMouseButtonReleased;
            window.MouseMoved += OnMouseMoved;
            window.KeyPressed += OnKeyPressed;
            window.KeyReleased += OnKeyReleased;
        }

        // Called once per frame to handle continuous actions.
        public void Update(float deltaTime)
        {
            // InputManager now only handles view panning
            // Game-specific input is handled in each game's Update method

            // Decay press timers using reusable list to avoid allocations
            _keysToRemove.Clear();
            foreach (var kvp in _pressTimers)
            {
                if (kvp.Value - deltaTime <= 0)
                {
                    _keysToRemove.Add(kvp.Key);
                }
            }

            // Update remaining timers and remove expired ones
            for (int i = 0; i < _keysToRemove.Count; i++)
            {
                _pressTimers.Remove(_keysToRemove[i]);
            }

            // Decay remaining timers
            var keys = _pressTimers.Keys;
            foreach (var key in keys)
            {
                _pressTimers[key] -= deltaTime;
            }
        }

        /// <summary>
        /// Consumes a buffered key press so it won't trigger again.
        /// Call this after handling a "Pressed" event to prevent double-triggering.
        /// </summary>
        /// <param name="key">Key name: "Enter", "Escape", "Space", "Tab", "Backspace", "Left", "Right", "Up", "Down"</param>
        public void ConsumePress(string key)
        {
            _pressTimers.Remove(key);
        }

        private void OnMouseButtonPressed(object sender, MouseButtonEventArgs e)
        {
            // Track mouse button state
            if (e.Button == Mouse.Button.Left)
            {
                _leftMouseDown = true;
            }
            else if (e.Button == Mouse.Button.Right)
            {
                _rightMouseDown = true;
            }
            else if (e.Button == Mouse.Button.Middle)
            {
                IsPanning = true;
                PanStartPos = new Vector2(e.X, e.Y);
            }
        }

        private void OnMouseButtonReleased(object sender, MouseButtonEventArgs e)
        {
            // Track mouse button state
            if (e.Button == Mouse.Button.Left)
            {
                _leftMouseDown = false;
            }
            else if (e.Button == Mouse.Button.Right)
            {
                _rightMouseDown = false;
            }
            else if (e.Button == Mouse.Button.Middle)
            {
                IsPanning = false;
            }
        }

        private void OnMouseMoved(object sender, MouseMoveEventArgs e)
        {
            MousePosition = window.MapPixelToCoords(new Vector2i(e.X, e.Y), view).ToSystemNumerics();

            // Handle view panning
            if (IsPanning)
            {
                Vector2i prevPixelPos = new Vector2i((int)PanStartPos.X, (int)PanStartPos.Y);
                Vector2i currentPixelPos = new Vector2i(e.X, e.Y);
                Vector2 worldPrev = window.MapPixelToCoords(prevPixelPos, view).ToSystemNumerics();
                Vector2 worldCurrent = window.MapPixelToCoords(currentPixelPos, view).ToSystemNumerics();
                Vector2 delta = worldPrev - worldCurrent;
                view.Center += delta.ToSfml();
                PanStartPos = new Vector2(e.X, e.Y);
            }
        }

        private void OnKeyPressed(object sender, KeyEventArgs e)
        {
            // Track keyboard state for games to poll
            switch (e.Code)
            {
                case Keyboard.Key.Space:
                    if (!keyState.Space) _pressTimers["Space"] = PRESS_BUFFER_TIME;
                    keyState.Space = true;
                    break;
                case Keyboard.Key.Escape:
                    if (!keyState.Escape) _pressTimers["Escape"] = PRESS_BUFFER_TIME;
                    keyState.Escape = true;
                    break;
                case Keyboard.Key.Left:
                    if (!keyState.Left) _pressTimers["Left"] = PRESS_BUFFER_TIME;
                    keyState.Left = true;
                    break;
                case Keyboard.Key.Right:
                    if (!keyState.Right) _pressTimers["Right"] = PRESS_BUFFER_TIME;
                    keyState.Right = true;
                    break;
                case Keyboard.Key.Up:
                    if (!keyState.Up) _pressTimers["Up"] = PRESS_BUFFER_TIME;
                    keyState.Up = true;
                    break;
                case Keyboard.Key.Down:
                    if (!keyState.Down) _pressTimers["Down"] = PRESS_BUFFER_TIME;
                    keyState.Down = true;
                    break;
                case Keyboard.Key.Enter:
                    if (!keyState.Enter) _pressTimers["Enter"] = PRESS_BUFFER_TIME;
                    keyState.Enter = true;
                    break;
                case Keyboard.Key.Tab:
                    if (!keyState.Tab) _pressTimers["Tab"] = PRESS_BUFFER_TIME;
                    keyState.Tab = true;
                    break;
                case Keyboard.Key.Backspace:
                    if (!keyState.Backspace) _pressTimers["Backspace"] = PRESS_BUFFER_TIME;
                    keyState.Backspace = true;
                    break;
            }
        }

        private void OnKeyReleased(object sender, KeyEventArgs e)
        {
            switch (e.Code)
            {
                case Keyboard.Key.Space:
                    keyState.Space = false;
                    break;
                case Keyboard.Key.Escape:
                    keyState.Escape = false;
                    break;
                case Keyboard.Key.Left:
                    keyState.Left = false;
                    break;
                case Keyboard.Key.Right:
                    keyState.Right = false;
                    break;
                case Keyboard.Key.Up:
                    keyState.Up = false;
                    break;
                case Keyboard.Key.Down:
                    keyState.Down = false;
                    break;
                case Keyboard.Key.Enter:
                    keyState.Enter = false;
                    break;
                case Keyboard.Key.Tab:
                    keyState.Tab = false;
                    break;
                case Keyboard.Key.Backspace:
                    keyState.Backspace = false;
                    break;
            }
        }

                /// <summary>
                /// Returns the current mouse position in world coordinates.
                /// </summary>
                public Vector2 GetMousePosition() => MousePosition;

                /// <summary>
                /// Returns a snapshot of the current key states with edge detection.
                /// The caller can poll this method each frame to determine which keys are pressed.
                /// "Pressed" properties are true only on the first frame a key/button is pressed (edge detection).
                /// </summary>
                public KeyState GetKeyState()
                {
                    // Build state directly - KeyState is now a struct, so no heap allocation
                    KeyState currentState;

                    // Held states
                    currentState.Left = keyState.Left;
                    currentState.Right = keyState.Right;
                    currentState.Up = keyState.Up;
                    currentState.Down = keyState.Down;
                    currentState.Space = keyState.Space;
                    currentState.Escape = keyState.Escape;
                    currentState.Enter = keyState.Enter;
                    currentState.Tab = keyState.Tab;
                    currentState.Backspace = keyState.Backspace;

                    // Buffered pressed states (active for 150ms after initial press)
                    currentState.LeftPressed = _pressTimers.TryGetValue("Left", out var leftTime) && leftTime > 0;
                    currentState.RightPressed = _pressTimers.TryGetValue("Right", out var rightTime) && rightTime > 0;
                    currentState.UpPressed = _pressTimers.TryGetValue("Up", out var upTime) && upTime > 0;
                    currentState.DownPressed = _pressTimers.TryGetValue("Down", out var downTime) && downTime > 0;
                    currentState.SpacePressed = _pressTimers.TryGetValue("Space", out var spaceTime) && spaceTime > 0;
                    currentState.EscapePressed = _pressTimers.TryGetValue("Escape", out var escapeTime) && escapeTime > 0;
                    currentState.EnterPressed = _pressTimers.TryGetValue("Enter", out var enterTime) && enterTime > 0;
                    currentState.TabPressed = _pressTimers.TryGetValue("Tab", out var tabTime) && tabTime > 0;
                    currentState.BackspacePressed = _pressTimers.TryGetValue("Backspace", out var backspaceTime) && backspaceTime > 0;

                    // Mouse button states
                    currentState.LeftMouseDown = _leftMouseDown;
                    currentState.RightMouseDown = _rightMouseDown;
                    currentState.MiddleMouseDown = IsPanning;
                    currentState.LeftMousePressed = _leftMouseDown && !_prevLeftMouseDown;
                    currentState.RightMousePressed = _rightMouseDown && !_prevRightMouseDown;
                    currentState.MousePosition = MousePosition;

                    // Store current mouse state as previous for next frame
                    _prevLeftMouseDown = _leftMouseDown;
                    _prevRightMouseDown = _rightMouseDown;

                    // Store current key state as previous for next frame (struct copy, no allocation)
                    prevKeyState.Left = keyState.Left;
                    prevKeyState.Right = keyState.Right;
                    prevKeyState.Up = keyState.Up;
                    prevKeyState.Down = keyState.Down;
                    prevKeyState.Space = keyState.Space;
                    prevKeyState.Escape = keyState.Escape;
                    prevKeyState.Enter = keyState.Enter;
                    prevKeyState.Tab = keyState.Tab;
                    prevKeyState.Backspace = keyState.Backspace;

                    return currentState;
                }
            }
        }