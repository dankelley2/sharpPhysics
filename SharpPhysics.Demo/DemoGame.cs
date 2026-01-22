#nullable enable
using System.Numerics;
using physics.Engine;
using physics.Engine.Classes.ObjectTemplates;
using physics.Engine.Core;
using physics.Engine.Input;
using physics.Engine.Rendering;
using SharpPhysics.Demo.Helpers;
using SharpPhysics.Demo.Integration;
using SharpPhysics.Demo.Settings;
using SharpPhysics.Engine.Player;

namespace SharpPhysics.Demo;

/// <summary>
/// Demo game showcasing SharpPhysics engine capabilities.
/// Includes physics balls, player controller, and person detection integration.
/// </summary>
public class DemoGame : IGame
{
    private GameEngine _engine = null!;
    private ObjectTemplates _objectTemplates = null!;
    private PlayerController _playerController = null!;
    private PersonColliderBridge? _personColliderBridge;

    public void Initialize(GameEngine engine)
    {
        _engine = engine;
        _objectTemplates = new ObjectTemplates(engine.PhysicsSystem);

        // Show debug UI for sandbox mode
        _engine.Renderer.ShowDebugUI = true;

        InitializeWorld(engine.WindowWidth, engine.WindowHeight);
        InitializePersonDetection(engine.WindowWidth, engine.WindowHeight);
    }

    private void InitializeWorld(uint worldWidth, uint worldHeight)
    {
        // Create walls
        _objectTemplates.CreateWall(new Vector2(0, 0), 15, (int)worldHeight);
        _objectTemplates.CreateWall(new Vector2((int)worldWidth - 15, 0), 15, (int)worldHeight);
        _objectTemplates.CreateWall(new Vector2(0, 0), (int)worldWidth, 15);
        _objectTemplates.CreateWall(new Vector2(0, (int)worldHeight - 15), (int)worldWidth, 15);

        // Create a grid of medium balls
        for (int i = 0; i < 1000; i += 50)
        {
            for (int j = 0; j < 600; j += 50)
            {
                if (j % 80 == 0)
                    _objectTemplates.CreateMedBall(i + 210, j + 40);
                else
                    _objectTemplates.CreateMedBall(i + 200, j + 40);
            }
        }

        // Create player
        var player = _objectTemplates.CreatePolygonCapsule(new Vector2(50, 20));
        _playerController = new PlayerController(player);
    }

    private void InitializePersonDetection(uint worldWidth, uint worldHeight)
    {
        var settings = GameSettings.Instance;

        try
        {
            _personColliderBridge = new PersonColliderBridge(
                physicsSystem: _engine.PhysicsSystem,
                worldWidth: worldWidth,
                worldHeight: worldHeight,
                modelPath: settings.ModelPath,
                flipX: settings.FlipX,
                flipY: settings.FlipY,
                ballRadius: settings.SandboxBallRadius,
                smoothingFactor: settings.SandboxSmoothingFactor,
                maxPeople: settings.MaxPeople
            );

            _personColliderBridge.OnError += (s, ex) => { Console.WriteLine($"Person Detection Error: {ex.Message}"); };

            _personColliderBridge.OnPersonBodyUpdated += (s, balls) =>
            {
                // Uncomment for debug output:
                // Console.WriteLine($"Tracking balls updated: {balls.Count} active");
            };

            // Start detection using configured camera source
            if (settings.CameraSourceType == "url")
            {
                _personColliderBridge.Start(
                    url: settings.CameraUrl,
                    width: settings.CameraWidth,
                    height: settings.CameraHeight,
                    fps: settings.CameraFps);
            }
            else
            {
                _personColliderBridge.Start(
                    cameraIndex: settings.CameraDeviceIndex,
                    width: settings.CameraWidth,
                    height: settings.CameraHeight,
                    fps: settings.CameraFps);
            }

            Console.WriteLine("Person detection initialized successfully.");
            Console.WriteLine($"Model: {Path.GetFullPath(settings.ModelPath)}");
            Console.WriteLine($"Camera: {(settings.CameraSourceType == "url" ? settings.CameraUrl : $"Device {settings.CameraDeviceIndex}")}");
            Console.WriteLine($"Tracking balls: radius={settings.SandboxBallRadius}, smoothing={settings.SandboxSmoothingFactor:F2}");
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
        // Check for ESC to return to menu
        if (keyState.Escape)
        {
            _engine.SwitchGame(new MenuGame());
            return;
        }

        // Update player controller
        _playerController.Update(keyState);

        // Process person detection updates (thread-safe)
        _personColliderBridge?.ProcessPendingUpdates();
    }

    public void Render(Renderer renderer)
    {
        // Draw skeleton overlay
        SkeletonRenderer.DrawSkeleton(renderer, _personColliderBridge);
    }

    public void Shutdown()
    {
        _personColliderBridge?.Dispose();
    }
}
