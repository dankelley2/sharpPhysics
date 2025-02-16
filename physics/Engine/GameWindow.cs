using SFML.Graphics;
using SFML.System;
using SFML.Window;
using System;
using System.Diagnostics;
using physics.Engine.Classes;
using physics.Engine.Classes.ObjectTemplates;
using physics.Engine.Structs;
using physics.Engine.Shaders;
using System.Drawing;
using Font = SFML.Graphics.Font;
using Color = SFML.Graphics.Color;

namespace physics.Engine
{
    public class GameWindow
    {
        private readonly RenderWindow window;
        private readonly PhysicsSystem physicsSystem = new PhysicsSystem();
        private readonly Clock clock = new Clock();
        private readonly Stopwatch stopwatch = new Stopwatch(); // Keep for performance metrics
        private bool isMousePressedLeft = false;
        private bool isMousePressedRight = false;
        private Vector2f startPoint;
        private Vector2f endPoint;
        private bool isGrabbing = false;
        private Vector2f mousePos;
        private long msFrameTime;
        private long msLastFrame;
        private long msPerDrawCycle;
        private long msThisFrame;
        private long msPhysicsTime;
        private int radius = 10;
        private Font debugFont;
        private Text debugText;

        public GameWindow(uint width, uint height, string title)
        {
            window = new RenderWindow(new VideoMode(width, height), title);
            window.Closed += (s, e) => window.Close();
            window.MouseButtonPressed += OnMouseButtonPressed;
            window.MouseButtonReleased += OnMouseButtonReleased;
            window.MouseMoved += OnMouseMoved;
            window.KeyPressed += OnKeyPressed;

            debugFont = new Font(@"C:\Windows\Fonts\arial.ttf"); // You'll need to provide a font file
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

            for (int i = 0; i < 400; i += 20)
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

                msPhysicsTime = stopwatch.ElapsedMilliseconds;
                float deltaTime = clock.Restart().AsSeconds();
                Update(deltaTime);
                msPhysicsTime = stopwatch.ElapsedMilliseconds - msPhysicsTime;

                Render();

                msPerDrawCycle = stopwatch.ElapsedMilliseconds - msFrameTime;
                msLastFrame = msThisFrame;
                msThisFrame = stopwatch.ElapsedMilliseconds;
                msFrameTime = msThisFrame - msLastFrame;
            }
        }

        private void Render()
        {
            window.Clear(Color.Black);

            // Debug info
            debugText.DisplayedString = $"ms physics time: {msPhysicsTime}\n" +
                                      $"ms total draw time: {msPerDrawCycle}\n" +
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
            if (isGrabbing)
            {
                physicsSystem.HoldActiveAtPoint(new Vector2f { X = mousePos.X, Y = mousePos.Y });
            }
            physicsSystem.Tick(deltaTime);
        }

        private void OnMouseButtonPressed(object sender, MouseButtonEventArgs e)
        {
            if (e.Button == Mouse.Button.Left)
            {
                var point = new Vector2f(e.X, e.Y);
                if (physicsSystem.ActivateAtPoint(point))
                {
                    isGrabbing = true;
                    return;
                }

                startPoint = new Vector2f(e.X, e.Y);
                isMousePressedLeft = true;
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
                    physicsSystem.SetVelocityOfActive(new Vector2f { X = 0, Y = 0 });
                    physicsSystem.ReleaseActiveObject();
                    isGrabbing = false;
                    return;
                }

                if (isMousePressedLeft)
                {
                    endPoint = new Vector2f(e.X, e.Y);
                    ActionTemplates.launch(
                        physicsSystem,
                        ObjectTemplates.CreateSmallBall(startPoint.X, startPoint.Y),
                        new Vector2f(startPoint.X, startPoint.Y),
                        new Vector2f(endPoint.X, endPoint.Y));
                    isMousePressedLeft = false;
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
                case Keyboard.Key.I:
                    ActionTemplates.changeShader(physicsSystem, new SFMLBallVelocityShader());
                    break;
                case Keyboard.Key.P:
                    ActionTemplates.changeShader(physicsSystem, new SFMLBallShader());
                    break;
                case Keyboard.Key.W:
                    ActionTemplates.changeShader(physicsSystem, new SFMLBallVelocityShader());
                    break;
                case Keyboard.Key.V:
                    ActionTemplates.changeShader(physicsSystem, new SFMLBallVelocityShader());
                    break;
                case Keyboard.Key.F:
                    ActionTemplates.changeShader(physicsSystem, new SFMLBallShader());
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
