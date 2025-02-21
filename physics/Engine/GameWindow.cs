using SFML.Graphics;
using SFML.System;
using SFML.Window;
using System;
using System.Diagnostics;
using physics.Engine.Classes;
using physics.Engine.Classes.ObjectTemplates;
using physics.Engine.Input;
using physics.Engine.Rendering;
using physics.Engine.Constraints;

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
            // Create context settings with antialiasing
            ContextSettings settings = new ContextSettings();
            settings.AntialiasingLevel = 8; // You can adjust this value as needed

            window = new RenderWindow(new VideoMode(width, height), title, Styles.Close, settings);
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
            ObjectTemplates.CreateWall(new Vector2f(0, 0), 15, (int)window.Size.Y);
            ObjectTemplates.CreateWall(new Vector2f((int)window.Size.X - 15, 0), 15, (int)window.Size.Y);
            ObjectTemplates.CreateWall(new Vector2f(0, 0), (int)window.Size.X, 15);
            ObjectTemplates.CreateWall(new Vector2f(0, (int)window.Size.Y - 15), (int)window.Size.X, 15);

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
            // Origin vector (top left)
            var boxAOrigin = new Vector2f(100, 400);
            var boxA = ObjectTemplates.CreateBox(boxAOrigin, 100, 100);
            boxA.Velocity = new Vector2f(0, 0);
            boxA.Angle = (float)(Math.PI / 4);

           // // Create the chassis as a box.
           // var chassisOrigin = new Vector2f(400, 400);
           // var chassis = ObjectTemplates.CreateBox(chassisOrigin, 500, 100);
           // chassis.CanRotate = true; // Allow chassis rotation
           // chassis.Angle = 0;

           // //Create two wheels as circles.
           //var leftWheel = ObjectTemplates.CreateMedBall(400, 550);
           // leftWheel.CanRotate = true;

           // // Create constraints linking the wheels to the chassis.
           // // For each wheel, we attach its center to a fixed point on the chassis along the vertical axis.
           // Vector2f leftAnchorChassis = new Vector2f(-250, 100);  // local offset from chassis center
           // Vector2f leftAnchorWheel = new Vector2f(0, 0);         // wheel's center
            //var leftWheelConstraint = new AxisConstraint(chassis, leftWheel, leftAnchorChassis, leftAnchorWheel);

            // Add the constraints to your physics system.
            //physicsSystem.Constraints.Add(leftWheelConstraint);

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
