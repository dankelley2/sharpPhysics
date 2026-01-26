#nullable enable
using System.Numerics;
using System.Runtime.ConstrainedExecution;
using physics.Engine;
using physics.Engine.Classes.ObjectTemplates;
using physics.Engine.Constraints;
using physics.Engine.Core;
using physics.Engine.Helpers;
using physics.Engine.Input;
using physics.Engine.Objects;
using physics.Engine.Rendering;
using physics.Engine.Shaders;
using SFML.Graphics;
using SFML.Window;
using SharpPhysics.Demo.DemoProps;
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
    private PrefabLoader _prefabLoader = null!;

    // Props
    private DemoGameCar? DemoCar;
    private readonly List<PrefabInstance> _loadedPrefabs = new();

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

    // Weld/Axis constraint creation state
    private enum ConstraintMode { None, Weld, Axis }
    private ConstraintMode _constraintMode = ConstraintMode.None;
    private PhysicsObject? _firstSelectedObject;
    private Vector2 _firstClickWorldPos; // Store the click position for axis constraints

    public void Initialize(GameEngine engine)
    {
        _engine = engine;
        _objectTemplates = new ObjectTemplates(engine.PhysicsSystem);
        _actionTemplates = new ActionTemplates(engine.PhysicsSystem, _objectTemplates);
        _prefabLoader = new PrefabLoader(engine, _objectTemplates);

        // Show debug UI for sandbox mode
        _engine.Renderer.ShowDebugUI = true;

        // Subscribe to window mouse/keyboard events for sandbox input
        _engine.Renderer.Window.MouseButtonPressed += OnMouseButtonPressed;
        _engine.Renderer.Window.MouseButtonReleased += OnMouseButtonReleased;
        _engine.Renderer.Window.MouseMoved += OnMouseMoved;
        _engine.Renderer.Window.KeyPressed += OnKeyPressed;

        InitializeWorld(engine.WindowWidth, engine.WindowHeight);

        // Enable or disable body detection
        if (GameSettings.Instance.PoseTrackingEnabled)
        {
            InitializePersonDetection(engine.WindowWidth, engine.WindowHeight);
        }

        // Print help for new features
        PrintControls();
    }

    private void PrintControls()
    {
        Console.WriteLine("=== DemoGame Controls ===");
        Console.WriteLine("  W - Weld mode (click two objects to weld)");
        Console.WriteLine("  A - Axis mode (click rotation center on body, then wheel)");
        Console.WriteLine("  Q - Cancel constraint mode");
        Console.WriteLine("  L - Load prefab at mouse position");
        Console.WriteLine("  ESC - Return to menu");
        Console.WriteLine("========================");
    }

    private void InitializeWorld(uint worldWidth, uint worldHeight)
    {
        // Create walls
        _objectTemplates.CreateWall(new Vector2(0, 0), 15, (int)worldHeight);
        _objectTemplates.CreateWall(new Vector2((int)worldWidth - 15, 0), 15, (int)worldHeight);
        _objectTemplates.CreateWall(new Vector2(0, 0), (int)worldWidth, 15);
        _objectTemplates.CreateWall(new Vector2(0, (int)worldHeight - 15), (int)worldWidth, 15);

        // Create player
        var player = _objectTemplates.CreatePolygonCapsule(new Vector2(50, 20));
        _playerController = new PlayerController(player);

        // Demo objects
        CreateCarDemo(worldWidth, worldHeight);
        CreateDemoBridge();
        CreateDemoChain();
        CreateDemoSproket();
        CreateConcavePolygonDemo();
    }

    private void CreateConcavePolygonDemo()
    {
        // L-shaped concave polygon (decomposed into convex pieces and welded)
        var lShapeVertices = new Vector2[]
        {
                new Vector2(0, 0),
                new Vector2(60, 0),
                new Vector2(60, 25),
                new Vector2(25, 25),
                new Vector2(25, 60),
                new Vector2(0, 60)
        };
        var lShape = _objectTemplates.CreateConcavePolygon(new Vector2(600, 100), lShapeVertices, canRotate: true, canBreak: true);

        _ = _objectTemplates.CreatePolygonTriangle(new Vector2(400, 200));

        // Arrow-shaped concave polygon
        var arrowVertices = new Vector2[]
        {
            new Vector2(0, 20),      // Left notch top
            new Vector2(30, 20),     // Shaft top-left
            new Vector2(30, 0),      // Arrow head left
            new Vector2(60, 25),     // Arrow tip
            new Vector2(30, 50),     // Arrow head right
            new Vector2(30, 30),     // Shaft bottom-left
            new Vector2(0, 30)       // Left notch bottom
        };
        var arrow = _objectTemplates.CreateConcavePolygon(new Vector2(700, 100), arrowVertices, canRotate: true, canBreak: true);

        // Star-like concave polygon (5-pointed, simplified)
        var starVertices = new Vector2[]
        {
            new Vector2(25, 0),      // Top point
            new Vector2(30, 18),     // Inner right-top
            new Vector2(50, 18),     // Right point
            new Vector2(35, 30),     // Inner right-bottom
            new Vector2(40, 50),     // Bottom-right point
            new Vector2(25, 38),     // Inner bottom
            new Vector2(10, 50),     // Bottom-left point
            new Vector2(15, 30),     // Inner left-bottom
            new Vector2(0, 18),      // Left point
            new Vector2(20, 18)      // Inner left-top
        };

        _ = _objectTemplates.CreateConcavePolygon(new Vector2(500, 100), starVertices, canRotate: true, canBreak: true);
    }

    private void CreateCarDemo(uint worldWidth, uint worldHeight)
    {
        // Car body dimensions
        float bodyWidth = 120f;
        float bodyHeight = 30f;
        float wheelRadius = 20f;
        float wheelInset = 10f; // Distance from body edge to wheel center

        // Position car in center-ish area
        float carX = worldWidth / 2f - bodyWidth / 2f;
        float carY = worldHeight - 200f; // Near bottom but above floor

        // Create the car body (box)
        var carBody = _objectTemplates.CreateBox(new Vector2(carX, carY), (int)bodyWidth, (int)bodyHeight);

        // Wheel X positions (in local body coordinates, where 0 = body center)
        float frontWheelLocalX = bodyWidth / 2f - wheelInset;   // Right side: +50
        float rearWheelLocalX = -bodyWidth / 2f + wheelInset;   // Left side: -50

        // Wheel world positions for spawning
        float frontWheelWorldX = carX + bodyWidth / 2f + frontWheelLocalX; // carX + 60 + 50 = carX + 110
        float rearWheelWorldX = carX + bodyWidth / 2f + rearWheelLocalX;   // carX + 60 - 50 = carX + 10
        float wheelWorldY = carY + bodyHeight + wheelRadius;

        // Create wheels (CreateMedBall takes top-left corner, ball radius is ~10)
        var frontWheel = _objectTemplates.CreateLargeBall(frontWheelWorldX - 10, wheelWorldY - 10);
        var rearWheel = _objectTemplates.CreateLargeBall(rearWheelWorldX - 10, wheelWorldY - 10);

        // Local anchors on body (relative to body center)
        Vector2 frontAttachOnBody = new Vector2(frontWheelLocalX, bodyHeight / 2f + wheelRadius);
        Vector2 rearAttachOnBody = new Vector2(rearWheelLocalX, bodyHeight / 2f + wheelRadius);

        _engine.AddAxisConstraint(carBody, frontWheel, frontAttachOnBody, Vector2.Zero);
        _engine.AddAxisConstraint(carBody, rearWheel, rearAttachOnBody, Vector2.Zero);

        // === WELDED PARTS (rigid attachments) ===

        // Spoiler on top-rear of car
        float spoilerWidth = 40f;
        float spoilerHeight = 8f;
        // Position spoiler so its center aligns with where we want to attach
        float spoilerLocalX = -bodyWidth / 2f + 75f;  // X offset from body center
        float spoilerLocalY = -bodyHeight / 2f - spoilerHeight / 2f - 2f;  // Just above body
        float spoilerWorldX = carX + bodyWidth + spoilerLocalX - spoilerWidth / 2f;
        float spoilerWorldY = carY + bodyHeight / 2f + spoilerLocalY - spoilerHeight / 2f;
        var spoiler = _objectTemplates.CreateBox(new Vector2(spoilerWorldX, spoilerWorldY), (int)spoilerWidth, (int)spoilerHeight);
        spoiler.Angle = 10f; // Tilted angle
        // Weld spoiler to body - use CENTER of spoiler (0,0) to minimize rotational coupling
        Vector2 spoilerAttachOnBody = new Vector2(spoilerLocalX, spoilerLocalY);
        Vector2 spoilerAttachOnSpoiler = Vector2.Zero;  // Center of spoiler

        // Add Weld
        _engine.AddWeldConstraint(carBody, spoiler, spoilerAttachOnBody, spoilerAttachOnSpoiler);

        // Front bumper
        float bumperWidth = 10f;
        float bumperHeight = 20f;
        float bumperWorldX = carX + bodyWidth; // At front (right side)
        float bumperWorldY = carY + 5f;
        var frontBumper = _objectTemplates.CreateBox(new Vector2(bumperWorldX, bumperWorldY), (int)bumperWidth, (int)bumperHeight);

        // Weld bumper to body
        // Local anchor on body: front-center edge
        Vector2 bumperAttachOnBody = new Vector2(bodyWidth / 2f, 0f);
        // Local anchor on bumper: left-center edge
        Vector2 bumperAttachOnBumper = new Vector2(-bumperWidth / 2f, 0f);

        // Add Weld
        _engine.AddWeldConstraint(carBody, frontBumper, bumperAttachOnBody, bumperAttachOnBumper);

        // Rear bumper
        float rearBumperWorldX = carX - bumperWidth;
        var rearBumper = _objectTemplates.CreateBox(new Vector2(rearBumperWorldX, bumperWorldY), (int)bumperWidth, (int)bumperHeight);

        // Weld rear bumper to body
        Vector2 rearBumperAttachOnBody = new Vector2(-bodyWidth / 2f, 0f);
        Vector2 rearBumperAttachOnBumper = new Vector2(bumperWidth / 2f, 0f);

        // Add Weld
        _engine.AddWeldConstraint(carBody, rearBumper, rearBumperAttachOnBody, rearBumperAttachOnBumper);

        DemoCar = new DemoGameCar(carBody, frontWheel, rearWheel, frontBumper, rearBumper, false);

    }

    private void CreateDemoSproket()
    {


        // circle of circles
        Vector2 center = new Vector2(800, 300);
        int numBalls = 22;
        float radius = 80f;
        PhysicsObject? firstBall = null;
        PhysicsObject? prevBall = null;
        for (int i = 0; i < numBalls; i++)
        {
            float angle = i * (2 * MathF.PI / numBalls);
            Vector2 pos = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            var ball = _objectTemplates.CreateMedBall(pos.X - 10, pos.Y - 10);
            if (i == 0)
                firstBall = ball;
            if (prevBall != null)
            {
                _engine.AddWeldConstraint(prevBall, ball, true);
            }
            prevBall = ball;
        }
        // Close the circle
        if (firstBall != null && prevBall != null)
        {
            _engine.AddWeldConstraint(prevBall, firstBall, true);
        }
    }

    private void CreateDemoChain()
    {
        // Add Chain of small balls as rope test
        PhysicsObject? prevObject2 = null;
        for (int i = 0; i < 12; i++)
        {

            var currentObj = _objectTemplates.CreateMedBall(150 + (i * 25), 300);

            // anchor
            if (i == 0)
            {
                var anchor = _objectTemplates.CreateBox(new Vector2(125, 290), 20, 20);
                anchor.Locked = true;
                // first one weld to anchor
                _engine.AddAxisConstraint(anchor, currentObj);
            }

            // axis constraint to previous
            if (i > 0 && prevObject2 != null)
            {
                _engine.AddAxisConstraint(prevObject2, currentObj);//, Vector2.Zero, new Vector2(-25, 0));
            }

            // end anchor
            if (i == 11)
            {
                var anchor = _objectTemplates.CreateBox(new Vector2(150 + (i * 25), 290), 20, 20);
                anchor.Locked = true;
                _engine.AddAxisConstraint(currentObj, anchor);

            }

            prevObject2 = currentObj;
        }
    }

    private void CreateDemoBridge()
    {

        // Add Chain of small balls as rope test
        PhysicsObject? prevObject = null;
        for (int i = 0; i < 12; i++)
        {

            var currentObj = _objectTemplates.CreateMedBall(150 + (i * 25), 150);

            if (prevObject != null)
            {
                //_engine.AddWeldConstraint(prevObject, currentObj, Vector2.Zero, new Vector2(-25, 0));
                _engine.AddWeldConstraint(prevObject, currentObj);

            }

            prevObject = currentObj;
        }
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

        // Process object updates for demo car
        DemoCar?.Update(deltaTime, keyState);
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

        // Draw constraint mode indicator
        if (_constraintMode != ConstraintMode.None)
        {
            string modeText = _constraintMode == ConstraintMode.Weld ? "WELD MODE" : "AXIS MODE";
            string stateText = _firstSelectedObject == null
                ? (_constraintMode == ConstraintMode.Axis ? "Click rotation point on body" : "Click first object")
                : "Click second object (Q to cancel)";
            renderer.DrawText($"{modeText}: {stateText}", 10, 40, 16, Color.Yellow);
        }
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

    private void HandleConstraintClick(Vector2 worldPos)
    {
        // Try to find an object at the click position
        if (!_engine.PhysicsSystem.ActivateAtPoint(worldPos))
        {
            Console.WriteLine("No object found at click position");
            return;
        }

        var clickedObject = _engine.PhysicsSystem.ActiveObject;
        _engine.PhysicsSystem.ReleaseActiveObject();

        if (_firstSelectedObject == null)
        {
            // First click - store the object and click position (the weld/pivot point)
            _firstSelectedObject = clickedObject;
            _firstClickWorldPos = worldPos;
            string modeHint = _constraintMode == ConstraintMode.Weld
                ? "weld point" : "pivot point";
            Console.WriteLine($"Selected object at {modeHint} {worldPos} - now click second object");
        }
        else
        {
            // Second click - create the constraint
            if (clickedObject == _firstSelectedObject)
            {
                Console.WriteLine("Cannot connect object to itself. Select a different object.");
                return;
            }

            if (_constraintMode == ConstraintMode.Weld)
            {
                // Weld: Both anchors point to the same world position (the first click point)
                // Convert world offset to LOCAL space by un-rotating by object's current angle
                Vector2 worldOffsetA = _firstClickWorldPos - _firstSelectedObject.Center;
                Vector2 worldOffsetB = _firstClickWorldPos - clickedObject.Center;
                Vector2 localAnchorA = PhysMath.RotateVector(worldOffsetA, -_firstSelectedObject.Angle);
                Vector2 localAnchorB = PhysMath.RotateVector(worldOffsetB, -clickedObject.Angle);
                _engine.AddWeldConstraint(_firstSelectedObject, clickedObject, localAnchorA, localAnchorB);
                Console.WriteLine($"Created Weld constraint at point {_firstClickWorldPos}");
            }
            else if (_constraintMode == ConstraintMode.Axis)
            {
                // Axis: Both anchors point to the same world position (the first click point)
                // Convert world offset to LOCAL space by un-rotating by object's current angle
                Vector2 worldOffsetA = _firstClickWorldPos - _firstSelectedObject.Center;
                Vector2 worldOffsetB = _firstClickWorldPos - clickedObject.Center;
                Vector2 localAnchorA = PhysMath.RotateVector(worldOffsetA, -_firstSelectedObject.Angle);
                Vector2 localAnchorB = PhysMath.RotateVector(worldOffsetB, -clickedObject.Angle);
                _engine.AddAxisConstraint(_firstSelectedObject, clickedObject, localAnchorA, localAnchorB);
                Console.WriteLine($"Created Axis constraint at pivot point {_firstClickWorldPos}");
            }

            // Reset state for next constraint
            _firstSelectedObject = null;
            _firstClickWorldPos = Vector2.Zero;
            // Stay in constraint mode for more connections
            Console.WriteLine($"Ready for next {(_constraintMode == ConstraintMode.Weld ? "weld" : "axis")} - click first object or press Q to exit");
        }
    }

    #region Physics Sandbox Input Handlers

    private void OnMouseButtonPressed(object? sender, MouseButtonEventArgs e)
    {
        Vector2 worldPos = _engine.Renderer.Window.MapPixelToCoords(
                new SFML.System.Vector2i(e.X, e.Y),
                _engine.Renderer.GameView).ToSystemNumerics();

        // Check if debug UI handles the click first
        if (e.Button == Mouse.Button.Left && _engine.Renderer.ShowDebugUI)
        {
            // Get UI position for debug UI
            Vector2 uiPos = _engine.Renderer.Window.MapPixelToCoords(
                new SFML.System.Vector2i(e.X, e.Y),
                _engine.Renderer.UiView).ToSystemNumerics();

            if (_engine.Renderer.DebugUiManager.HandleClick(uiPos))
            {
                return; // UI handled the click
            }
        }

        if (e.Button == Mouse.Button.Left)
        {
            // Handle constraint creation mode
            if (_constraintMode != ConstraintMode.None)
            {
                HandleConstraintClick(worldPos);
                return;
            }

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
        // Stop debug UI drag operations
        if (e.Button == Mouse.Button.Left)
        {
            _engine.Renderer.DebugUiManager.StopDrag();
        }

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

        // Handle debug UI drag
        if (_engine.Renderer.DebugUiManager.DraggedElement != null)
        {
            Vector2 uiPos = _engine.Renderer.Window.MapPixelToCoords(
                new SFML.System.Vector2i(e.X, e.Y),
                _engine.Renderer.UiView).ToSystemNumerics();
            _engine.Renderer.DebugUiManager.HandleDrag(uiPos);
        }

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
            case Keyboard.Key.Q:
                // Exit constraint mode
                if (_constraintMode != ConstraintMode.None)
                {
                    _constraintMode = ConstraintMode.None;
                    _firstSelectedObject = null;
                    Console.WriteLine("Constraint mode cancelled");
                }
                break;
            case Keyboard.Key.W:
                // Toggle Weld constraint mode
                if (_constraintMode == ConstraintMode.Weld)
                {
                    _constraintMode = ConstraintMode.None;
                    _firstSelectedObject = null;
                    Console.WriteLine("Weld mode disabled");
                }
                else
                {
                    _constraintMode = ConstraintMode.Weld;
                    _firstSelectedObject = null;
                    Console.WriteLine("Weld mode enabled - click first object");
                }
                break;
            case Keyboard.Key.A:
                // Toggle Axis constraint mode
                if (_constraintMode == ConstraintMode.Axis)
                {
                    _constraintMode = ConstraintMode.None;
                    _firstSelectedObject = null;
                    Console.WriteLine("Axis mode disabled");
                }
                else
                {
                    _constraintMode = ConstraintMode.Axis;
                    _firstSelectedObject = null;
                    Console.WriteLine("Axis mode enabled - click first object (click point = rotation center)");
                }
                break;
            case Keyboard.Key.L:
                // Load prefab at mouse position
                LoadPrefabAtMouse();
                break;
        }
    }

    private void LoadPrefabAtMouse()
    {
        var prefabFiles = PrefabLoader.GetAvailablePrefabs();
        if (prefabFiles.Length == 0)
        {
            Console.WriteLine("No prefabs available. Create some in the Prefab Designer first!");
            return;
        }

        Console.WriteLine("Available prefabs:");
        for (int i = 0; i < prefabFiles.Length; i++)
        {
            Console.WriteLine($"  {i + 1}: {Path.GetFileNameWithoutExtension(prefabFiles[i])}");
        }

        // Load the most recent prefab by default
        var prefabPath = prefabFiles[^1];
        var instance = _prefabLoader.LoadPrefab(prefabPath, _mousePosition);
        if (instance != null)
        {
            _loadedPrefabs.Add(instance);
            Console.WriteLine($"Loaded prefab '{instance.Name}' at position {_mousePosition}");
        }
    }

    #endregion
}
