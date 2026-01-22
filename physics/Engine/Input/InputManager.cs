using SFML.Graphics;
using SFML.System;
using SFML.Window;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using physics.Engine.Helpers;
using physics.Engine.Rendering.UI;

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
        private const float PRESS_BUFFER_TIME = 0.15f; // Keys stay "pressed" for 150ms

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

            // Decay press timers
            var keys = _pressTimers.Keys.ToList();
            foreach (var key in keys)
            {
                _pressTimers[key] -= deltaTime;
                if (_pressTimers[key] <= 0)
                {
                    _pressTimers.Remove(key);
                }
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
            Vector2 worldPos = window.MapPixelToCoords(new Vector2i(e.X, e.Y), view).ToSystemNumerics();

            // UI click handling priority:
            foreach (var ui in UiElement.GlobalUiElements)
            {
                if (ui.HandleClick(worldPos))
                    return; // UI handled the click; do not proceed further.
            }

            // Only handle middle mouse for panning - games handle their own input
            if (e.Button == Mouse.Button.Middle)
            {
                IsPanning = true;
                PanStartPos = new Vector2(e.X, e.Y);
            }
        }

        private void OnMouseButtonReleased(object sender, MouseButtonEventArgs e)
        {
            // Stop any ongoing UI drag operations
            UiElement.StopDrag();

            // Only handle middle mouse release for panning
            if (e.Button == Mouse.Button.Middle)
            {
                IsPanning = false;
            }
        }

        private void OnMouseMoved(object sender, MouseMoveEventArgs e)
        {
            MousePosition = window.MapPixelToCoords(new Vector2i(e.X, e.Y), view).ToSystemNumerics();

            // Handle UI drag events
            if (UiElement.DraggedElement != null)
            {
                UiElement.HandleDrag(MousePosition);
                return;
            }

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
                                        // Calculate buffered pressed states (check timers for 150ms buffer)
                                        var currentState = new KeyState
                                        {
                                            // Held states
                                            Left = keyState.Left,
                                            Right = keyState.Right,
                                            Up = keyState.Up,
                                            Down = keyState.Down,
                                            Space = keyState.Space,
                                            Escape = keyState.Escape,
                                            Enter = keyState.Enter,
                                            Tab = keyState.Tab,
                                            Backspace = keyState.Backspace,

                                            // Buffered pressed states (active for 150ms after initial press)
                                            LeftPressed = _pressTimers.ContainsKey("Left") && _pressTimers["Left"] > 0,
                                            RightPressed = _pressTimers.ContainsKey("Right") && _pressTimers["Right"] > 0,
                                            UpPressed = _pressTimers.ContainsKey("Up") && _pressTimers["Up"] > 0,
                                            DownPressed = _pressTimers.ContainsKey("Down") && _pressTimers["Down"] > 0,
                                            SpacePressed = _pressTimers.ContainsKey("Space") && _pressTimers["Space"] > 0,
                                            EscapePressed = _pressTimers.ContainsKey("Escape") && _pressTimers["Escape"] > 0,
                                            EnterPressed = _pressTimers.ContainsKey("Enter") && _pressTimers["Enter"] > 0,
                                            TabPressed = _pressTimers.ContainsKey("Tab") && _pressTimers["Tab"] > 0,
                                            BackspacePressed = _pressTimers.ContainsKey("Backspace") && _pressTimers["Backspace"] > 0,

                                            // Mouse state (games can handle their own if needed)
                                            LeftMouseDown = false,
                                            RightMouseDown = false,
                                            MiddleMouseDown = IsPanning,
                                            LeftMousePressed = false,
                                            RightMousePressed = false,
                                            MousePosition = MousePosition
                                        };

                                        // Store current state as previous for next frame
                                        prevKeyState = new KeyState
                                        {
                                            Left = keyState.Left,
                                            Right = keyState.Right,
                                            Up = keyState.Up,
                                            Down = keyState.Down,
                                            Space = keyState.Space,
                                            Escape = keyState.Escape,
                                            Enter = keyState.Enter,
                                            Tab = keyState.Tab,
                                            Backspace = keyState.Backspace
                                        };

                                        return currentState;
                                    }
                                }
                        }