﻿using SFML.Graphics;
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
        private Vector2f endPoint;
        private bool isGrabbing = false;
        private Vector2f mousePos;

        // Timing variables
        private long msFrameTime;
        private long msPerDrawCycle;
        private long msPhysicsTime;

        private int radius = 10;
        private Font debugFont;
        private Text debugText;

        // Cap maximum delta time to avoid spiral of death (e.g., 33ms ~30 FPS)
        private const float MAX_DELTA_TIME = 0.033f;

        // Timer accumulator for continuous ball launching (every 0.25 seconds)
        private float launchTimer = 0f;

        public GameWindow(uint width, uint height, string title)
        {
            window = new RenderWindow(new VideoMode(width, height), title, Styles.Close);
            window.Closed += (s, e) => window.Close();
            window.MouseButtonPressed += OnMouseButtonPressed;
            window.MouseButtonReleased += OnMouseButtonReleased;
            window.MouseMoved += OnMouseMoved;
            window.KeyPressed += OnKeyPressed;

            window.SetFramerateLimit(144);

            debugFont = new Font(@"C:\Windows\Fonts\arial.ttf"); // Provide a valid font file
            debugText = new Text("", debugFont, 12);
            debugText.FillColor = Color.White;
            debugText.Position = new Vector2f(40, 40);

            InitializeGame();
            stopwatch.Start();
        }

        private void InitializeGame()
        {
            ObjectTemplates.CreateWall(0, 0, 5, (int)window.Size.Y);
            ObjectTemplates.CreateWall((int)window.Size.X - 5, 0, (int)window.Size.X, (int)window.Size.Y);
            ObjectTemplates.CreateWall(0, 0, (int)window.Size.X, 5);
            ObjectTemplates.CreateWall(0, (int)window.Size.Y - 5, (int)window.Size.X, (int)window.Size.Y);

            for (int i = 0; i < 800; i += 20)
            {
                for (int j = 0; j < 200; j += 20)
                {
                    ObjectTemplates.CreateMedBall(i + 200, j + 150);
                }
            }

            ObjectTemplates.CreateAttractor(400, 450);
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
            window.Clear(Color.Black);

            // Debug info
            debugText.DisplayedString = $"ms physics time: {msPhysicsTime}\n" +
                                          $"ms draw time: {msPerDrawCycle}\n" +
                                          $"frame rate: {1000 / Math.Max(msFrameTime, 1)}\n" +
                                          $"num objects: {PhysicsSystem.ListStaticObjects.Count}";
            window.Draw(debugText);

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
                    // Compute the launch vector implicitly from (mousePos - startPoint)
                    // Launch balls from the current mouse position.
                    // (Optionally, you could add a slight offset to each ball if desired.)
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
            if (e.Button == Mouse.Button.Left)
            {
                var point = new Vector2f(e.X, e.Y);
                // If clicking on an existing object, grab it.
                if (physicsSystem.ActivateAtPoint(point))
                {
                    isGrabbing = true;
                    return;
                }
                // Otherwise, start the ball launching stream.
                startPoint = point;
                isMousePressedLeft = true;
                launchTimer = 0f;
            }

            if (e.Button == Mouse.Button.Right)
            {
                var point = new Vector2f(e.X, e.Y);
                if (physicsSystem.ActivateAtPoint(point))
                {
                    physicsSystem.RemoveActiveObject();
                }
                isMousePressedRight = true;
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

            if (e.Button == Mouse.Button.Right)
            {
                isMousePressedRight = false;
            }
        }

        private void OnMouseMoved(object sender, MouseMoveEventArgs e)
        {
            mousePos = new Vector2f(e.X, e.Y);

            if (isMousePressedRight)
            {
                if (physicsSystem.ActivateAtPoint(mousePos))
                {
                    physicsSystem.RemoveActiveObject();
                }
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
                    //ActionTemplates.changeShader(physicsSystem, new SFMLBallVelocityShader());
                    break;
                case Keyboard.Key.G:
                    ObjectTemplates.CreateAttractor((int)mousePos.X, (int)mousePos.Y);
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
