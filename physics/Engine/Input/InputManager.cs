using SFML.Graphics;
using SFML.System;
using SFML.Window;
using System;
using physics.Engine.Classes;
using physics.Engine.Classes.ObjectTemplates;
using physics.Engine.Shaders;
using physics.Engine.Objects;

namespace physics.Engine.Input
{
    public class InputManager
    {
        private RenderWindow window;
        private PhysicsSystem physicsSystem;
        private View view;

        // Input state properties.
        public bool IsGrabbing { get; private set; } = false;
        public bool IsMousePressedLeft { get; private set; } = false;
        public bool IsMousePressedRight { get; private set; } = false;
        public bool IsCreatingBox { get; private set; } = false;
        public bool IsPanning { get; private set; } = false;

        public Vector2f StartPoint { get; private set; }
        public Vector2f MousePosition { get; private set; }
        public Vector2f BoxStartPoint { get; private set; }
        public Vector2f BoxEndPoint { get; private set; }
        public Vector2f PanStartPos { get; private set; }

        private float launchTimer = 0f;
        private const float LaunchInterval = 0.035f;
        private const float playerMoveSpeed = 100f; // adjust speed as needed

        // Key state for polling important keys.
        private KeyState keyState = new KeyState();

        public InputManager(RenderWindow window, PhysicsSystem physicsSystem, View view)
        {
            this.window = window;
            this.physicsSystem = physicsSystem;
            this.view = view;

            // Subscribe to window events.
            window.MouseButtonPressed += OnMouseButtonPressed;
            window.MouseButtonReleased += OnMouseButtonReleased;
            window.MouseMoved += OnMouseMoved;
            window.MouseWheelScrolled += OnMouseWheelScrolled;
            window.KeyPressed += OnKeyPressed;
            window.KeyReleased += OnKeyReleased;
        }

        // Called once per frame to handle continuous actions.
        public void Update(float deltaTime)
        {
            if (IsGrabbing)
            {
                physicsSystem.HoldActiveAtPoint(MousePosition);
            }
            else if (IsMousePressedLeft)
            {
                launchTimer += deltaTime;
                if (launchTimer >= LaunchInterval)
                {
                    ActionTemplates.launch(
                        physicsSystem,
                        ObjectTemplates.CreateMedBall(StartPoint.X, StartPoint.Y),
                        StartPoint,
                        MousePosition);
                    launchTimer = 0f;
                }
            }
        }

        private void OnMouseButtonPressed(object sender, MouseButtonEventArgs e)
        {
            Vector2f worldPos = window.MapPixelToCoords(new Vector2i(e.X, e.Y), view);

            if (e.Button == Mouse.Button.Left)
            {
                if (physicsSystem.ActivateAtPoint(worldPos))
                {
                    IsGrabbing = true;
                    return;
                }
                StartPoint = worldPos;
                IsMousePressedLeft = true;
                launchTimer = 0f;
            }
            else if (e.Button == Mouse.Button.Right)
            {
                bool objectFound = physicsSystem.ActivateAtPoint(worldPos);
                if (objectFound)
                {
                    if (IsGrabbing)
                    {
                        PhysicsSystem.ActiveObject.CanRotate = false;
                        PhysicsSystem.ActiveObject.Locked = true;
                    }
                    else
                    {
                        physicsSystem.RemoveActiveObject();
                    }
                }
                else
                {
                    IsCreatingBox = true;
                    BoxStartPoint = worldPos;
                    BoxEndPoint = worldPos;
                }
                IsMousePressedRight = true;
            }
            else if (e.Button == Mouse.Button.Middle)
            {
                IsPanning = true;
                PanStartPos = new Vector2f(e.X, e.Y);
            }
        }

        private void OnMouseButtonReleased(object sender, MouseButtonEventArgs e)
        {
            if (e.Button == Mouse.Button.Left)
            {
                if (IsGrabbing)
                {
                    physicsSystem.ReleaseActiveObject();
                    IsGrabbing = false;
                    return;
                }
                if (IsMousePressedLeft)
                {
                    IsMousePressedLeft = false;
                    launchTimer = 0f;
                }
            }
            else if (e.Button == Mouse.Button.Right)
            {
                if (IsCreatingBox)
                {
                    float minX = Math.Min(BoxStartPoint.X, BoxEndPoint.X);
                    float minY = Math.Min(BoxStartPoint.Y, BoxEndPoint.Y);
                    float maxX = Math.Max(BoxStartPoint.X, BoxEndPoint.X);
                    float maxY = Math.Max(BoxStartPoint.Y, BoxEndPoint.Y);
                    ObjectTemplates.CreateBox(new Vector2f(minX, minY), (int)maxX - (int)minX, (int)maxY - (int)minY);
                    IsCreatingBox = false;
                }
                IsMousePressedRight = false;
            }
            else if (e.Button == Mouse.Button.Middle)
            {
                IsPanning = false;
            }
        }

        private void OnMouseMoved(object sender, MouseMoveEventArgs e)
        {
            MousePosition = window.MapPixelToCoords(new Vector2i(e.X, e.Y), view);

            if (IsPanning)
            {
                Vector2i prevPixelPos = new Vector2i((int)PanStartPos.X, (int)PanStartPos.Y);
                Vector2i currentPixelPos = new Vector2i(e.X, e.Y);
                Vector2f worldPrev = window.MapPixelToCoords(prevPixelPos, view);
                Vector2f worldCurrent = window.MapPixelToCoords(currentPixelPos, view);
                Vector2f delta = worldPrev - worldCurrent;
                view.Center += delta;
                PanStartPos = new Vector2f(e.X, e.Y);
            }

            if (IsCreatingBox)
            {
                BoxEndPoint = MousePosition;
            }
            else if (IsMousePressedRight)
            {
                Vector2f worldPos = window.MapPixelToCoords(new Vector2i(e.X, e.Y), view);
                if (physicsSystem.ActivateAtPoint(worldPos))
                {
                    physicsSystem.RemoveActiveObject();
                }
            }
        }

        private void OnMouseWheelScrolled(object sender, MouseWheelScrollEventArgs e)
        {
            Console.WriteLine("Scroll delta: " + e.Delta);
            //Uncomment to enable zooming:
             if (e.Delta > 0)
            {
                view.Zoom(0.9f);
            }
            else
            {
                view.Zoom(1.1f);
            }
        }

        private void OnKeyPressed(object sender, KeyEventArgs e)
        {
            switch (e.Code)
            {
                case Keyboard.Key.Space:
                    keyState.Space = true;
                    physicsSystem.FreezeStaticObjects();
                    break;
                case Keyboard.Key.P:
                    ActionTemplates.changeShader(physicsSystem, new SFMLPolyShader());
                    break;
                case Keyboard.Key.V:
                    // Optionally switch shaders.
                    break;
                case Keyboard.Key.G:
                    ObjectTemplates.CreateAttractor(MousePosition.X, MousePosition.Y);
                    break;
                case Keyboard.Key.Semicolon:
                    ActionTemplates.PopAndMultiply(physicsSystem);
                    break;
                case Keyboard.Key.Escape:
                    window.Close();
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
            }
        }

        private void OnKeyReleased(object sender, KeyEventArgs e)
        {
            switch (e.Code)
            {
                case Keyboard.Key.Space:
                    keyState.Space = false;
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
                // Add other keys if necessary.
            }
        }

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
                Space = keyState.Space
            };
        }
    }
}