using SFML.Graphics;
using SFML.System;
using SFML.Window;
using System;
using System.Diagnostics;
using physics.Engine.Classes;
using physics.Engine.Classes.ObjectTemplates;
using physics.Engine.Input;
using physics.Engine.Rendering;

namespace physics.Engine
{
    public class GameWindow
    {
        private RenderWindow window;
        private PhysicsSystem physicsSystem = new PhysicsSystem();
        private Clock clock = new Clock();
        private Stopwatch stopwatch = new Stopwatch();

        // Cap maximum delta time to avoid spiral of death.
        private const float MAX_DELTA_TIME = 0.033f;

        // Performance metrics.
        private long msFrameTime;
        private long msDrawTime;
        private long msPhysicsTime;

        private View view;
        private InputManager inputManager;
        private Renderer renderer;

        public GameWindow(uint width, uint height, string title)
        {
            window = new RenderWindow(new VideoMode(width, height), title, Styles.Close);
            window.Closed += (s, e) => window.Close();

            // Create and set a view covering the whole window.
            view = new View(new FloatRect(0, 0, width, height));
            window.SetFramerateLimit(144);

            // Instantiate the input manager and renderer.
            inputManager = new InputManager(window, physicsSystem, view);
            renderer = new Renderer(window, view, physicsSystem);

            InitializeGame();
            stopwatch.Start();
        }

        private void InitializeGame()
        {
            // Create walls.
            ObjectTemplates.CreateWall(0, 0, 15, (int)window.Size.Y);
            ObjectTemplates.CreateWall((int)window.Size.X - 15, 0, (int)window.Size.X, (int)window.Size.Y);
            ObjectTemplates.CreateWall(0, 0, (int)window.Size.X, 15);
            ObjectTemplates.CreateWall(0, (int)window.Size.Y - 15, (int)window.Size.X, (int)window.Size.Y);

            // Create a grid of medium balls.
            for (int i = 0; i < 800; i += 20)
            {
                for (int j = 0; j < 200; j += 20)
                {
                    ObjectTemplates.CreateMedBall(i + 200, j + 150);
                }
            }

            // Create an attractor.
            ObjectTemplates.CreateAttractor(400, 450);

            // Create a box with initial velocity and angle.
            var boxA = ObjectTemplates.CreateBox(100, 400, 200, 500);
            boxA.Velocity = new Vector2f(0, 100);
            boxA.Angle = (float)(Math.PI / 4);
        }

        public void Run()
        {
            while (window.IsOpen)
            {
                window.DispatchEvents();

                long frameStartTime = stopwatch.ElapsedMilliseconds;

                // Get delta time and cap it.
                float deltaTime = clock.Restart().AsSeconds();
                deltaTime = Math.Min(deltaTime, MAX_DELTA_TIME);

                // Update input-related actions (ball launching, object grabbing, etc.).
                long physicsStart = stopwatch.ElapsedMilliseconds;
                inputManager.Update(deltaTime);
                physicsSystem.Tick(deltaTime);
                msPhysicsTime = stopwatch.ElapsedMilliseconds - physicsStart;

                // Render the current frame.
                long renderStart = stopwatch.ElapsedMilliseconds;
                renderer.Render(msPhysicsTime, msDrawTime, msFrameTime,
                                  inputManager.IsCreatingBox,
                                  inputManager.BoxStartPoint,
                                  inputManager.BoxEndPoint);
                msDrawTime = stopwatch.ElapsedMilliseconds - renderStart;

                msFrameTime = stopwatch.ElapsedMilliseconds - frameStartTime;
            }
        }
    }
}
