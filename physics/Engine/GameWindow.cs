using SFML.Graphics;
using SFML.System;
using SFML.Window;
using System;
using System.Diagnostics;
using physics.Engine.Classes;
using physics.Engine.Classes.ObjectTemplates;
using physics.Engine.Structs;
using physics.Engine.Shaders;
using Font = SFML.Graphics.Font;
using Color = SFML.Graphics.Color;

namespace physics.Engine
{
    public class GameWindow
    {
        private readonly RenderWindow window;
        private readonly PhysicsSystem physicsSystem = new PhysicsSystem();
        private readonly Clock clock = new Clock();
        private readonly Stopwatch stopwatch = new Stopwatch(); // For performance metrics

        private bool isMousePressedLeft = false;
        private bool isMousePressedRight = false;
        private Vector2f startPoint;
        private bool isGrabbing = false;
        private Vector2f mousePos;

        // New fields for box creation with the RMB.
        private bool isCreatingBox = false;
        private Vector2f boxStartPoint;
        private Vector2f boxEndPoint;

        // Timing variables
        private long msFrameTime;
        private long msPerDrawCycle;
        private long msPhysicsTime;

        private Font debugFont;
        private Text debugText;

        // Cap maximum delta time to avoid spiral of death (e.g., 33ms ~30 FPS)
        private const float MAX_DELTA_TIME = 0.033f;

        // Timer accumulator for continuous ball launching (every 0.25 seconds)
        private float launchTimer = 0f;

        // New fields for view (zooming & panning)
        private View view;
        private bool isPanning = false;
        private Vector2f panStartPos; // stored as pixel coordinates

        public GameWindow(uint width, uint height, string title)
        {
            window = new RenderWindow(new VideoMode(width, height), title, Styles.Close);
            window.Closed += (s, e) => window.Close();
            window.MouseButtonPressed += OnMouseButtonPressed;
            window.MouseButtonReleased += OnMouseButtonReleased;
            window.MouseMoved += OnMouseMoved;
            window.MouseWheelScrolled += OnMouseWheelScrolled; // For zooming
            window.KeyPressed += OnKeyPressed;

            window.SetFramerateLimit(144);

            // Create a new view that covers the whole window.
            view = new View(new FloatRect(0, 0, width, height));

            debugFont = new Font(@"C:\Windows\Fonts\arial.ttf"); // Provide a valid font file
            debugText = new Text("", debugFont, 12);
            debugText.FillColor = Color.White;
            debugText.Position = new Vector2f(40, 40);

            InitializeGame();
            stopwatch.Start();
        }

        private void InitializeGame()
        {
            ObjectTemplates.CreateWall(0, 0, 15, (int)window.Size.Y);
            ObjectTemplates.CreateWall((int)window.Size.X - 15, 0, (int)window.Size.X, (int)window.Size.Y);
            ObjectTemplates.CreateWall(0, 0, (int)window.Size.X, 15);
            ObjectTemplates.CreateWall(0, (int)window.Size.Y - 15, (int)window.Size.X, (int)window.Size.Y);

            for (int i = 0; i < 800; i += 20)
            {
                for (int j = 0; j < 200; j += 20)
                {
                    ObjectTemplates.CreateMedBall(i + 200, j + 150);
                }
            }

            ObjectTemplates.CreateAttractor(400, 450);

            // Create a box
            var boxA = ObjectTemplates.CreateBox(100, 400, 200, 500);

            boxA.Velocity = new Vector2f(0, 100);
            boxA.Angle = (float)(Math.PI / 4);

            //var boxb = ObjectTemplates.CreateBox(190, 300, 290, 400);
        }

        public void Run()
        {
            while (window.IsOpen)
            {
                window.DispatchEvents();

                // Start of frame timestamp
                long frameStartTime = stopwatch.ElapsedMilliseconds;

                // Measure physics update time
                float deltaTime = clock.Restart().AsSeconds();
                // Cap deltaTime to avoid physics spiral of death.
                deltaTime = Math.Min(deltaTime, MAX_DELTA_TIME);

                long physicsStart = stopwatch.ElapsedMilliseconds;
                Update(deltaTime);
                msPhysicsTime = stopwatch.ElapsedMilliseconds - physicsStart;

                // Measure render time
                long renderStart = stopwatch.ElapsedMilliseconds;
                Render();
                msPerDrawCycle = stopwatch.ElapsedMilliseconds - renderStart;

                // Total frame time
                msFrameTime = stopwatch.ElapsedMilliseconds - frameStartTime;
            }
        }

        private void Render()
        {
            // Apply the current view (which might be zoomed or panned)
            window.SetView(view);

            window.Clear(Color.Black);

            // Debug info
            debugText.DisplayedString = $"ms physics time: {msPhysicsTime}\n" +
                                          $"ms draw time: {msPerDrawCycle}\n" +
                                          $"frame rate: {1000 / Math.Max(msFrameTime, 1)}\n" +
                                          $"num objects: {PhysicsSystem.ListStaticObjects.Count}";
            window.Draw(debugText);

            // Optionally, draw the rectangle preview when creating a box.
            if (isCreatingBox)
            {
                float minX = Math.Min(boxStartPoint.X, boxEndPoint.X);
                float minY = Math.Min(boxStartPoint.Y, boxEndPoint.Y);
                float width = Math.Abs(boxEndPoint.X - boxStartPoint.X);
                float height = Math.Abs(boxEndPoint.Y - boxStartPoint.Y);
                RectangleShape previewRect = new RectangleShape(new Vector2f(width, height));
                previewRect.Position = new Vector2f(minX, minY);
                previewRect.FillColor = new Color(0, 0, 0, 0);
                previewRect.OutlineColor = Color.Red;
                previewRect.OutlineThickness = 2;
                window.Draw(previewRect);
            }

            // Draw objects
            foreach (var obj in PhysicsSystem.ListStaticObjects)
            {
                var sfmlShader = obj.Shader;
                if (sfmlShader != null)
                {
                    sfmlShader.PreDraw(obj, window);
                    sfmlShader.Draw(obj, window);
                    sfmlShader.PostDraw(obj, window);
                }
            }

            window.Display();
        }

        private void Update(float deltaTime)
        {
            // If an object is being grabbed, update its position.
            if (isGrabbing)
            {
                physicsSystem.HoldActiveAtPoint(mousePos);
            }
            else if (isMousePressedLeft)
            {
                // Accumulate time while left mouse button is held.
                launchTimer += deltaTime;
                if (launchTimer >= 0.025f)
                {
                    // Launch balls from the starting point toward the current mouse position.
                    ActionTemplates.launch(
                        physicsSystem,
                        ObjectTemplates.CreateSmallBall(startPoint.X, startPoint.Y),
                        startPoint,
                        mousePos);

                    // Reset the timer.
                    launchTimer = 0f;
                }
            }

            physicsSystem.Tick(deltaTime);
        }

        private void OnMouseButtonPressed(object sender, MouseButtonEventArgs e)
        {
            // Translate the mouse position from pixel to world coordinates.
            Vector2f worldPos = window.MapPixelToCoords(new Vector2i(e.X, e.Y), view);

            if (e.Button == Mouse.Button.Left)
            {
                // If clicking on an existing object, grab it.
                if (physicsSystem.ActivateAtPoint(worldPos))
                {
                    isGrabbing = true;
                    return;
                }
                // Otherwise, start the ball launching stream.
                startPoint = worldPos;
                isMousePressedLeft = true;
                launchTimer = 0f;
            }
            else if (e.Button == Mouse.Button.Right)
            {
                // Check if there is an object to remove.
                bool objectFound = physicsSystem.ActivateAtPoint(worldPos);
                if (objectFound)
                {
                    physicsSystem.RemoveActiveObject();
                }
                else
                {
                    // Start creating a rectangle.
                    isCreatingBox = true;
                    boxStartPoint = worldPos;
                    boxEndPoint = worldPos;
                }
                isMousePressedRight = true;
            }
            else if (e.Button == Mouse.Button.Middle)
            {
                // Start panning.
                isPanning = true;
                // For panning, we keep the raw pixel position.
                panStartPos = new Vector2f(e.X, e.Y);
            }
        }

        private void OnMouseButtonReleased(object sender, MouseButtonEventArgs e)
        {
            if (e.Button == Mouse.Button.Left)
            {
                if (isGrabbing)
                {
                    physicsSystem.SetVelocityOfActive(new Vector2f(0, 0));
                    physicsSystem.ReleaseActiveObject();
                    isGrabbing = false;
                    return;
                }
                // Stop the continuous launch stream.
                if (isMousePressedLeft)
                {
                    isMousePressedLeft = false;
                    launchTimer = 0f;
                }
            }
            else if (e.Button == Mouse.Button.Right)
            {
                // If we were creating a box, finalize it.
                if (isCreatingBox)
                {
                    // Compute min and max coordinates.
                    float minX = Math.Min(boxStartPoint.X, boxEndPoint.X);
                    float minY = Math.Min(boxStartPoint.Y, boxEndPoint.Y);
                    float maxX = Math.Max(boxStartPoint.X, boxEndPoint.X);
                    float maxY = Math.Max(boxStartPoint.Y, boxEndPoint.Y);

                    // Create the box using the provided function.
                    ObjectTemplates.CreateBox(minX, minY, maxX, maxY);

                    isCreatingBox = false;
                }
                isMousePressedRight = false;
            }
            else if (e.Button == Mouse.Button.Middle)
            {
                // Stop panning.
                isPanning = false;
            }
        }

        private void OnMouseMoved(object sender, MouseMoveEventArgs e)
        {
            // Update the mouse position in world coordinates.
            mousePos = window.MapPixelToCoords(new Vector2i(e.X, e.Y), view);

            // Handle panning if the middle mouse button is held.
            if (isPanning)
            {
                // Use raw pixel positions for calculating the panning delta.
                Vector2i prevPixelPos = new Vector2i((int)panStartPos.X, (int)panStartPos.Y);
                Vector2i currentPixelPos = new Vector2i(e.X, e.Y);
                Vector2f worldPrev = window.MapPixelToCoords(prevPixelPos, view);
                Vector2f worldCurrent = window.MapPixelToCoords(currentPixelPos, view);
                Vector2f delta = worldPrev - worldCurrent;
                view.Center += delta;
                // Update the starting point for the next move.
                panStartPos = new Vector2f(e.X, e.Y);
            }

            // If we are creating a box with the RMB, update the box's end point.
            if (isCreatingBox)
            {
                boxEndPoint = mousePos;
            }
            else if (isMousePressedRight)
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
            // Adjust the view zoom based on the scroll delta.
            // Scrolling up zooms in, scrolling down zooms out.
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
                    physicsSystem.FreezeStaticObjects();
                    break;
                case Keyboard.Key.P:
                    ActionTemplates.changeShader(physicsSystem, new SFMLBallShader());
                    break;
                case Keyboard.Key.V:
                    // Uncomment or add shader changes as needed.
                    // ActionTemplates.changeShader(physicsSystem, new SFMLBallVelocityShader());
                    break;
                case Keyboard.Key.G:
                    ObjectTemplates.CreateAttractor(mousePos.X, mousePos.Y);
                    break;
                case Keyboard.Key.Semicolon:
                    ActionTemplates.PopAndMultiply(physicsSystem);
                    break;
                case Keyboard.Key.Escape:
                    window.Close();
                    break;
            }
        }
    }
}
