#nullable enable
using System;
using System.Numerics;
using physics.Engine;
using physics.Engine.Classes.ObjectTemplates;
using physics.Engine.Core;
using physics.Engine.Input;
using physics.Engine.Integration;
using physics.Engine.Rendering;
using SharpPhysics.Engine.Player;

namespace physics.Demo
{
    /// <summary>
    /// Demo game showcasing SharpPhysics engine capabilities.
    /// Includes physics balls, player controller, and person detection integration.
    /// </summary>
    public class DemoGame : IGame
    {
        private GameEngine _engine = null!;
        private PlayerController _playerController = null!;
        private PersonColliderBridge? _personColliderBridge;

        // Path to the ONNX model - YOLOv8-Pose model for hand/head tracking
        private const string MODEL_PATH = "models/yolo26s_pose.onnx";

        public void Initialize(GameEngine engine)
        {
            _engine = engine;

            InitializeWorld(engine.WindowWidth, engine.WindowHeight);
            InitializePersonDetection(engine.WindowWidth, engine.WindowHeight);
        }

        private void InitializeWorld(uint worldWidth, uint worldHeight)
        {
            // Create walls
            ObjectTemplates.CreateWall(new Vector2(0, 0), 15, (int)worldHeight);
            ObjectTemplates.CreateWall(new Vector2((int)worldWidth - 15, 0), 15, (int)worldHeight);
            ObjectTemplates.CreateWall(new Vector2(0, 0), (int)worldWidth, 15);
            ObjectTemplates.CreateWall(new Vector2(0, (int)worldHeight - 15), (int)worldWidth, 15);

            // Create a grid of medium balls
            for (int i = 0; i < 1000; i += 50)
            {
                for (int j = 0; j < 600; j += 50)
                {
                    if (j % 80 == 0)
                        ObjectTemplates.CreateMedBall(i + 210, j + 40);
                    else
                        ObjectTemplates.CreateMedBall(i + 200, j + 40);
                }
            }

            // Create player
            var player = ObjectTemplates.CreatePolygonCapsule(new Vector2(50, 20));
            _playerController = new PlayerController(player);
        }

        private void InitializePersonDetection(uint worldWidth, uint worldHeight)
        {
            try
            {
                _personColliderBridge = new PersonColliderBridge(
                    worldWidth: worldWidth,
                    worldHeight: worldHeight,
                    modelPath: MODEL_PATH,
                    flipX: true,         // Mirror mode for natural interaction
                    flipY: false,        // SharpPhysics uses Y-down coordinate system
                    trackingSpeed: 15f,  // How fast balls follow detected positions
                    ballRadius: 20,      // Radius of head/hand tracking balls
                    smoothingFactor: 0.5f // Smoothing to reduce jitter
                );

                _personColliderBridge.OnError += (s, ex) =>
                {
                    Console.WriteLine($"Person Detection Error: {ex.Message}");
                };

                _personColliderBridge.OnPersonBodyUpdated += (s, balls) =>
                {
                    // Uncomment for debug output:
                    // Console.WriteLine($"Tracking balls updated: {balls.Count} active");
                };

                // Start detection using webcam or default camera
                _personColliderBridge.Start(url: "http://192.168.1.161:8080", width: 640, height: 480, fps: 30);

                // Pass the bridge to the renderer for skeleton visualization
                _engine.Renderer.SetPersonColliderBridge(_personColliderBridge);

                Console.WriteLine("Person detection initialized successfully.");
                Console.WriteLine($"Model expected at: {System.IO.Path.GetFullPath(MODEL_PATH)}");
                Console.WriteLine("Tracking: Head, Left Hand, Right Hand (20 radius balls)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize person detection: {ex.Message}");
                Console.WriteLine("The application will continue without person detection.");
                _personColliderBridge = null;
            }
        }

        public void Update(float deltaTime, KeyState keyState)
        {
            // Update player controller
            _playerController.Update(keyState);

            // Process person detection updates (thread-safe)
            _personColliderBridge?.ProcessPendingUpdates();
        }

        public void Render(Renderer renderer)
        {
            // Game-specific rendering can be added here
            // The engine handles rendering physics objects
        }

        public void Shutdown()
        {
            _personColliderBridge?.Dispose();
        }
    }
}
