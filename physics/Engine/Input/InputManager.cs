using SFML.Graphics;
using SFML.System;
using SFML.Window;
using System;
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
                    keyState.Space = true;
                    break;
                case Keyboard.Key.Escape:
                    keyState.Escape = true;
                    break;
                case Keyboard.Key.Left:
                    keyState.Left = true;
                    break;
                case Keyboard.Key.Right:
                    keyState.Right = true;
                    break;
                case Keyboard.Key.Up:
                    keyState.Up = true;
                    break;
                case Keyboard.Key.Down:
                    keyState.Down = true;
                    break;
                case Keyboard.Key.Enter:
                    keyState.Enter = true;
                    break;
                case Keyboard.Key.Tab:
                    keyState.Tab = true;
                    break;
                case Keyboard.Key.Backspace:
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
                                /// Returns a snapshot of the current key states.
                                /// The caller can poll this method each frame to determine which keys are pressed.
                                /// </summary>
                                public KeyState GetKeyState()
                                {
                                    // Return a copy so external systems cannot modify internal state.
                                    return new KeyState
                                    {
                                        Left = keyState.Left,
                                        Right = keyState.Right,
                                        Up = keyState.Up,
                                        Down = keyState.Down,
                                        Space = keyState.Space,
                                        Escape = keyState.Escape,
                                        Enter = keyState.Enter,
                                        Tab = keyState.Tab,
                                        Backspace = keyState.Backspace,
                                        LeftMouseDown = false, // Games now handle their own mouse state
                                        RightMouseDown = false,
                                        MiddleMouseDown = IsPanning,
                                        LeftMousePressed = false,
                                        RightMousePressed = false,
                                        MousePosition = MousePosition
                                    };
                                }
                            }
                        }