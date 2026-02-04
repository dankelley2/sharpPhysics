#nullable enable
using System.Numerics;
using SharpPhysics.Engine.Classes.ObjectTemplates;
using SharpPhysics.Engine.Core;
using SharpPhysics.Engine.Helpers;
using SharpPhysics.Engine.Input;
using SharpPhysics.Engine.Objects;
using SFML.Graphics;
using SFML.Window;
using SharpPhysics.Demo.DemoProps;
using SharpPhysics.Demo.Helpers;
using SharpPhysics.Demo.Integration;
using SharpPhysics.Demo.Settings;
using SharpPhysics.Engine.Player;
using SharpPhysics.Rendering;

namespace SharpPhysics.Demo;

public class DemoGame : IGame
{
    private GameEngine _engine = null!;
    private ObjectTemplates _objectTemplates = null!;
    private ActionTemplates _actionTemplates = null!;
    private PlayerController _playerController = null!;
    private PersonColliderBridge? _personColliderBridge;
    private PrefabLoader _prefabLoader = null!;
    private AnimatedBackground _background = null!;
    private DemoSceneBuilder _sceneBuilder = null!;
    private SandboxDebugUI _debugUI = null!;

    private DemoGameCar? _demoCar;
    private readonly List<PrefabInstance> _loadedPrefabs = new();

    // Sandbox input state
    private bool _isGrabbing;
    private bool _isMousePressedLeft;
    private bool _isMousePressedRight;
    private bool _isCreatingBox;
    private Vector2 _startPoint;
    private Vector2 _mousePosition;
    private Vector2 _boxStartPoint;
    private Vector2 _boxEndPoint;
    private float _launchTimer;
    private const float LaunchInterval = 0.035f;

    // View panning
    private bool _isPanning;
    private Vector2 _panStartScreenPos;

    // Constraint creation
    private enum ConstraintMode { None, Weld, Axis }
    private ConstraintMode _constraintMode = ConstraintMode.None;
    private PhysicsObject? _firstSelectedObject;
    private Vector2 _firstClickWorldPos;

    public void Initialize(GameEngine engine)
    {
        _engine = engine;
        _objectTemplates = new ObjectTemplates(engine.PhysicsSystem);
        _actionTemplates = new ActionTemplates(engine.PhysicsSystem);
        _prefabLoader = new PrefabLoader(engine, _objectTemplates);
        _sceneBuilder = new DemoSceneBuilder(engine, _objectTemplates);
        _background = new AnimatedBackground(engine.WindowWidth, engine.WindowHeight);
        _debugUI = new SandboxDebugUI(engine);

        _engine.Renderer.Window.MouseButtonPressed += OnMouseButtonPressed;
        _engine.Renderer.Window.MouseButtonReleased += OnMouseButtonReleased;
        _engine.Renderer.Window.MouseMoved += OnMouseMoved;
        _engine.Renderer.Window.KeyPressed += OnKeyPressed;

        InitializeWorld(engine.WindowWidth, engine.WindowHeight);

        if (GameSettings.Instance.PoseTrackingEnabled)
            InitializePersonDetection(engine.WindowWidth, engine.WindowHeight);

        PrintControls();
    }

    private void PrintControls()
    {
        Console.WriteLine("=== DemoGame Controls ===");
        Console.WriteLine("  W - Weld mode | A - Axis mode | Q - Cancel");
        Console.WriteLine("  L - Load prefab | ESC - Return to menu");
        Console.WriteLine("========================");
    }

    private void InitializeWorld(uint worldWidth, uint worldHeight)
    {
        _sceneBuilder.CreateWalls(worldWidth, worldHeight);

        var player = _objectTemplates.CreatePolygonCapsule(new Vector2(50, 20));
        _playerController = new PlayerController(player);

        _demoCar = _sceneBuilder.CreateCar();
        //_sceneBuilder.CreateBridge(new Vector2(150, 150));
        //_sceneBuilder.CreateChain(new Vector2(150, 300));
        //_sceneBuilder.CreateSprocket(new Vector2(800, 300));
        //_sceneBuilder.CreateConcavePolygonDemo(new Vector2(600, 100));
        _sceneBuilder.CreateBlanket(new Vector2(700, 400));
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

            _personColliderBridge.OnError += (s, ex) => Console.WriteLine($"Person Detection Error: {ex.Message}");

            if (settings.CameraSourceType == "url")
                _personColliderBridge.Start(url: settings.CameraUrl);
            else
                _personColliderBridge.Start(
                    cameraIndex: settings.CameraDeviceIndex,
                    width: settings.CameraDeviceResolutionX,
                    height: settings.CameraDeviceResolutionY,
                    fps: settings.CameraDeviceFps);

            Console.WriteLine($"Person detection initialized. Model: {Path.GetFullPath(settings.ModelPath)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize person detection: {ex.Message}");
            _personColliderBridge = null;
        }
    }

    public void Update(float deltaTime, InputManager inputManager)
    {
        if (inputManager.IsKeyPressedBuffered(Keyboard.Key.Escape))
        {
            _engine.SwitchGame(new MenuGame());
            inputManager.ConsumeKeyPress(Keyboard.Key.Escape);
            return;
        }

        HandleViewPanning(inputManager);
        HandleScrollZoom(inputManager);
        HandleSandboxInput(deltaTime, inputManager);

        _playerController.Update(deltaTime, inputManager);
        _personColliderBridge?.ProcessPendingUpdates();
        _demoCar?.Update(deltaTime, inputManager);
        _background.Update(deltaTime);
    }

    private void HandleViewPanning(InputManager inputManager)
    {
        if (inputManager.IsMousePressed(Mouse.Button.Middle))
        {
            _isPanning = true;
            _panStartScreenPos = inputManager.MouseScreenPosition;
        }
        else if (!inputManager.IsMouseHeld(Mouse.Button.Middle) && _isPanning)
        {
            _isPanning = false;
        }

        if (_isPanning)
        {
            _engine.Renderer.PanViewByScreenDelta(_panStartScreenPos, inputManager.MouseScreenPosition);
            _panStartScreenPos = inputManager.MouseScreenPosition;
        }
    }

    private void HandleScrollZoom(InputManager inputManager)
    {
        if (Math.Abs(inputManager.ScrollWheelDelta) > 1e-6)
            _engine.Renderer.ZoomView(inputManager.ScrollWheelDelta, inputManager.MouseScreenPosition);
    }

    private void HandleSandboxInput(float deltaTime, InputManager inputManager)
    {
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
    }

    public void RenderBackground(Renderer renderer)
    {
        _background.Draw(renderer.Window);
    }

    public void Render(Renderer renderer)
    {
        if (_isCreatingBox)
        {
            float minX = Math.Min(_boxStartPoint.X, _boxEndPoint.X);
            float minY = Math.Min(_boxStartPoint.Y, _boxEndPoint.Y);
            float width = Math.Abs(_boxEndPoint.X - _boxStartPoint.X);
            float height = Math.Abs(_boxEndPoint.Y - _boxStartPoint.Y);
            renderer.DrawRectangle(new Vector2(minX, minY), new Vector2(width, height), new Color(0, 0, 0, 0), Color.Red, 2f);
        }

        SkeletonRenderer.DrawSkeleton(renderer, _personColliderBridge);

        if (_constraintMode != ConstraintMode.None)
        {
            string modeText = _constraintMode == ConstraintMode.Weld ? "WELD MODE" : "AXIS MODE";
            string stateText = _firstSelectedObject == null
                ? (_constraintMode == ConstraintMode.Axis ? "Click rotation point on body" : "Click first object")
                : "Click second object (Q to cancel)";
            renderer.DrawText($"{modeText}: {stateText}", 10, 170, 16, Color.Yellow);
        }

        _debugUI.Render(renderer);
    }

    public void Shutdown()
    {
        _engine.Renderer.Window.MouseButtonPressed -= OnMouseButtonPressed;
        _engine.Renderer.Window.MouseButtonReleased -= OnMouseButtonReleased;
        _engine.Renderer.Window.MouseMoved -= OnMouseMoved;
        _engine.Renderer.Window.KeyPressed -= OnKeyPressed;

        _debugUI.Clear();
        _background.Dispose();
        _personColliderBridge?.Dispose();
    }

    #region Input Handlers

    private void HandleConstraintClick(Vector2 worldPos)
    {
        if (!_engine.PhysicsSystem.ActivateAtPoint(worldPos))
        {
            Console.WriteLine("No object found at click position");
            return;
        }

        var clickedObject = _engine.PhysicsSystem.ActiveObject;
        _engine.PhysicsSystem.ReleaseActiveObject();

        if (_firstSelectedObject == null)
        {
            _firstSelectedObject = clickedObject;
            _firstClickWorldPos = worldPos;
            Console.WriteLine($"Selected first object - now click second object");
        }
        else
        {
            if (clickedObject == _firstSelectedObject)
            {
                Console.WriteLine("Cannot connect object to itself.");
                return;
            }

            Vector2 worldOffsetA = _firstClickWorldPos - _firstSelectedObject.Center;
            Vector2 worldOffsetB = _firstClickWorldPos - clickedObject.Center;
            Vector2 localAnchorA = PhysMath.RotateVector(worldOffsetA, -_firstSelectedObject.Angle);
            Vector2 localAnchorB = PhysMath.RotateVector(worldOffsetB, -clickedObject.Angle);

            if (_constraintMode == ConstraintMode.Weld)
                _engine.AddWeldConstraint(_firstSelectedObject, clickedObject, localAnchorA, localAnchorB);
            else if (_constraintMode == ConstraintMode.Axis)
                _engine.AddAxisConstraint(_firstSelectedObject, clickedObject, localAnchorA, localAnchorB);

            Console.WriteLine($"Created {_constraintMode} constraint");
            _firstSelectedObject = null;
            _firstClickWorldPos = Vector2.Zero;
        }
    }

    private void OnMouseButtonPressed(object? sender, MouseButtonEventArgs e)
    {
        Vector2 worldPos = _engine.Renderer.Window.MapPixelToCoords(
            new SFML.System.Vector2i(e.X, e.Y), _engine.Renderer.GameView).ToSystemNumerics();

        if (e.Button == Mouse.Button.Left)
        {
            Vector2 uiPos = _engine.Renderer.Window.MapPixelToCoords(
                new SFML.System.Vector2i(e.X, e.Y), _engine.Renderer.UiView).ToSystemNumerics();
            if (_debugUI.HandleClick(uiPos)) return;

            if (_constraintMode != ConstraintMode.None) { HandleConstraintClick(worldPos); return; }
            if (_engine.PhysicsSystem.ActivateAtPoint(worldPos)) { _isGrabbing = true; return; }
            _startPoint = worldPos;
            _isMousePressedLeft = true;
            _launchTimer = 0f;
        }
        else if (e.Button == Mouse.Button.Right)
        {
            if (_engine.PhysicsSystem.ActivateAtPoint(worldPos))
            {
                if (_isGrabbing) _engine.PhysicsSystem.ActiveObject.Locked = true;
                else _engine.PhysicsSystem.RemoveActiveObject();
            }
            else
            {
                _isCreatingBox = true;
                _boxStartPoint = _boxEndPoint = worldPos;
            }
            _isMousePressedRight = true;
        }
    }

    private void OnMouseButtonReleased(object? sender, MouseButtonEventArgs e)
    {
        if (e.Button == Mouse.Button.Left)
        {
            _debugUI.StopDrag();
            if (_isGrabbing) { _engine.PhysicsSystem.ReleaseActiveObject(); _isGrabbing = false; return; }
            if (_isMousePressedLeft) { _isMousePressedLeft = false; _launchTimer = 0f; }
        }
        else if (e.Button == Mouse.Button.Right)
        {
            if (_isCreatingBox)
            {
                float minX = Math.Min(_boxStartPoint.X, _boxEndPoint.X), minY = Math.Min(_boxStartPoint.Y, _boxEndPoint.Y);
                float maxX = Math.Max(_boxStartPoint.X, _boxEndPoint.X), maxY = Math.Max(_boxStartPoint.Y, _boxEndPoint.Y);
                _objectTemplates.CreateBox(new Vector2(minX, minY), (int)(maxX - minX), (int)(maxY - minY));
                _isCreatingBox = false;
            }
            _isMousePressedRight = false;
        }
    }

    private void OnMouseMoved(object? sender, MouseMoveEventArgs e)
    {
        _mousePosition = _engine.Renderer.Window.MapPixelToCoords(
            new SFML.System.Vector2i(e.X, e.Y), _engine.Renderer.GameView).ToSystemNumerics();

        if (_debugUI.IsDragging)
        {
            Vector2 uiPos = _engine.Renderer.Window.MapPixelToCoords(
                new SFML.System.Vector2i(e.X, e.Y), _engine.Renderer.UiView).ToSystemNumerics();
            _debugUI.HandleDrag(uiPos);
        }

        if (_isCreatingBox) _boxEndPoint = _mousePosition;
        else if (_isMousePressedRight && !_isCreatingBox && _engine.PhysicsSystem.ActivateAtPoint(_mousePosition))
            _engine.PhysicsSystem.RemoveActiveObject();
    }

    private void OnKeyPressed(object? sender, KeyEventArgs e)
    {
        switch (e.Code)
        {
            case Keyboard.Key.Space: _engine.PhysicsSystem.FreezeStaticObjects(); break;
            case Keyboard.Key.G: _objectTemplates.CreateAttractor(_mousePosition.X, _mousePosition.Y); break;
            case Keyboard.Key.Q:
                if (_constraintMode != ConstraintMode.None) { _constraintMode = ConstraintMode.None; _firstSelectedObject = null; }
                break;
            case Keyboard.Key.W:
                _constraintMode = _constraintMode == ConstraintMode.Weld ? ConstraintMode.None : ConstraintMode.Weld;
                _firstSelectedObject = null;
                break;
            case Keyboard.Key.A:
                _constraintMode = _constraintMode == ConstraintMode.Axis ? ConstraintMode.None : ConstraintMode.Axis;
                _firstSelectedObject = null;
                break;
            case Keyboard.Key.L: LoadPrefabAtMouse(); break;
        }
    }

    private void LoadPrefabAtMouse()
    {
        var prefabFiles = PrefabLoader.GetAvailablePrefabs();
        if (prefabFiles.Length == 0) { Console.WriteLine("No prefabs available."); return; }

        var prefabPath = prefabFiles[^1];
        var instance = _prefabLoader.LoadPrefab(prefabPath, _mousePosition);
        if (instance != null)
        {
            _loadedPrefabs.Add(instance);
            Console.WriteLine($"Loaded prefab '{instance.Name}' at {_mousePosition}");
        }
    }

    #endregion
}
