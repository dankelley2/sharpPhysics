#nullable enable
using System.Numerics;
using physics.Engine;
using physics.Engine.Classes.ObjectTemplates;
using physics.Engine.Core;
using physics.Engine.Helpers;
using physics.Engine.Input;
using physics.Engine.Rendering;
using physics.Engine.Shaders;
using SFML.Graphics;
using SFML.Window;
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
    private ActionTemplates _actionTemplates = null!;
    private PlayerController _playerController = null!;
    private PersonColliderBridge? _personColliderBridge;

    // Physics sandbox input state
    private bool _isGrabbing = false;
    private bool _isMousePressedLeft = false;
    private bool _isMousePressedRight = false;
    private bool _isCreatingBox = false;
    private Vector2 _startPoint;
    private Vector2 _mousePosition;
    private Vector2 _boxStartPoint;
    private Vector2 _boxEndPoint;
    private float _launchTimer = 0f;
    private const float LaunchInterval = 0.035f;

    public void Initialize(GameEngine engine)
    {
        _engine = engine;
        _objectTemplates = new ObjectTemplates(engine.PhysicsSystem);
        _actionTemplates = new ActionTemplates(engine.PhysicsSystem, _objectTemplates);

        // Show debug UI for sandbox mode
        _engine.Renderer.ShowDebugUI = true;

        // Subscribe to window mouse/keyboard events for sandbox input
        _engine.Renderer.Window.MouseButtonPressed += OnMouseButtonPressed;
        _engine.Renderer.Window.MouseButtonReleased += OnMouseButtonReleased;
        _engine.Renderer.Window.MouseMoved += OnMouseMoved;
        _engine.Renderer.Window.KeyPressed += OnKeyPressed;

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

        // Handle physics sandbox input - ball launching
        if (_isGrabbing)
        {
            _engine.PhysicsSystem.HoldActiveAtPoint(_mousePosition);
        }
        else if (_isMousePressedLeft)
        {
            _launchTimer += deltaTime;
            if (_launchTimer >= LaunchInterval)
            {
                _actionTemplates.Launch(
                    _objectTemplates.CreateMedBall(_startPoint.X, _startPoint.Y),
                    _startPoint,
                    _mousePosition);
                _launchTimer = 0f;
            }
        }

        // Update player controller
        _playerController.Update(keyState);

        // Process person detection updates (thread-safe)
        _personColliderBridge?.ProcessPendingUpdates();
    }

        public void Render(Renderer renderer)
        {
            // Draw box creation preview
            if (_isCreatingBox)
            {
                float minX = Math.Min(_boxStartPoint.X, _boxEndPoint.X);
                float minY = Math.Min(_boxStartPoint.Y, _boxEndPoint.Y);
                float width = Math.Abs(_boxEndPoint.X - _boxStartPoint.X);
                float height = Math.Abs(_boxEndPoint.Y - _boxStartPoint.Y);
                RectangleShape previewRect = new RectangleShape(new SFML.System.Vector2f(width, height))
                {
                    Position = new SFML.System.Vector2f(minX, minY),
                    FillColor = new Color(0, 0, 0, 0),
                    OutlineColor = Color.Red,
                    OutlineThickness = 2
                };
                renderer.Window.Draw(previewRect);
            }

            // Draw skeleton overlay
            SkeletonRenderer.DrawSkeleton(renderer, _personColliderBridge);
        }

        public void Shutdown()
        {
            // Unsubscribe from window events
            _engine.Renderer.Window.MouseButtonPressed -= OnMouseButtonPressed;
            _engine.Renderer.Window.MouseButtonReleased -= OnMouseButtonReleased;
            _engine.Renderer.Window.MouseMoved -= OnMouseMoved;
            _engine.Renderer.Window.KeyPressed -= OnKeyPressed;

            _personColliderBridge?.Dispose();
        }

        #region Physics Sandbox Input Handlers

        private void OnMouseButtonPressed(object? sender, MouseButtonEventArgs e)
        {
            Vector2 worldPos = _engine.Renderer.Window.MapPixelToCoords(
                new SFML.System.Vector2i(e.X, e.Y),
                _engine.Renderer.GameView).ToSystemNumerics();

            if (e.Button == Mouse.Button.Left)
            {
                if (_engine.PhysicsSystem.ActivateAtPoint(worldPos))
                {
                    _isGrabbing = true;
                    return;
                }
                _startPoint = worldPos;
                _isMousePressedLeft = true;
                _launchTimer = 0f;
            }
            else if (e.Button == Mouse.Button.Right)
            {
                bool objectFound = _engine.PhysicsSystem.ActivateAtPoint(worldPos);
                if (objectFound)
                {
                    if (_isGrabbing)
                    {
                        // Lock the grabbed object in place
                        _engine.PhysicsSystem.ActiveObject.Locked = true;
                    }
                    else
                    {
                        _engine.PhysicsSystem.RemoveActiveObject();
                    }
                }
                else
                {
                    _isCreatingBox = true;
                    _boxStartPoint = worldPos;
                    _boxEndPoint = worldPos;
                }
                _isMousePressedRight = true;
            }
        }

        private void OnMouseButtonReleased(object? sender, MouseButtonEventArgs e)
        {
            if (e.Button == Mouse.Button.Left)
            {
                if (_isGrabbing)
                {
                    _engine.PhysicsSystem.ReleaseActiveObject();
                    _isGrabbing = false;
                    return;
                }
                if (_isMousePressedLeft)
                {
                    _isMousePressedLeft = false;
                    _launchTimer = 0f;
                }
            }
            else if (e.Button == Mouse.Button.Right)
            {
                if (_isCreatingBox)
                {
                    float minX = Math.Min(_boxStartPoint.X, _boxEndPoint.X);
                    float minY = Math.Min(_boxStartPoint.Y, _boxEndPoint.Y);
                    float maxX = Math.Max(_boxStartPoint.X, _boxEndPoint.X);
                    float maxY = Math.Max(_boxStartPoint.Y, _boxEndPoint.Y);
                    _objectTemplates.CreateBox(new Vector2(minX, minY), (int)(maxX - minX), (int)(maxY - minY));
                    _isCreatingBox = false;
                }
                _isMousePressedRight = false;
            }
        }

        private void OnMouseMoved(object? sender, MouseMoveEventArgs e)
        {
            _mousePosition = _engine.Renderer.Window.MapPixelToCoords(
                new SFML.System.Vector2i(e.X, e.Y),
                _engine.Renderer.GameView).ToSystemNumerics();

            if (_isCreatingBox)
            {
                _boxEndPoint = _mousePosition;
            }
            else if (_isMousePressedRight && !_isCreatingBox)
            {
                if (_engine.PhysicsSystem.ActivateAtPoint(_mousePosition))
                {
                    _engine.PhysicsSystem.RemoveActiveObject();
                }
            }
        }

        private void OnKeyPressed(object? sender, KeyEventArgs e)
        {
            switch (e.Code)
            {
                case Keyboard.Key.Space:
                    _engine.PhysicsSystem.FreezeStaticObjects();
                    break;
                case Keyboard.Key.P:
                    _actionTemplates.ChangeShader(new SFMLPolyShader());
                    break;
                case Keyboard.Key.V:
                    _actionTemplates.ChangeShader(new SFMLPolyRainbowShader());
                    break;
                case Keyboard.Key.G:
                    _objectTemplates.CreateAttractor(_mousePosition.X, _mousePosition.Y);
                    break;
                case Keyboard.Key.Semicolon:
                    _actionTemplates.PopAndMultiply();
                    break;
            }
        }

        #endregion
    }
