#nullable enable
using System.Numerics;
using physics.Engine;
using physics.Engine.Core;
using physics.Engine.Input;
using physics.Engine.Rendering;
using physics.Engine.Rendering.UI;
using physics.Engine.Classes.ObjectTemplates;
using SharpPhysics.Demo.Helpers;
using SFML.Graphics;
using SFML.Window;

namespace SharpPhysics.Demo.Designer;

/// <summary>
/// Prefab Part Designer - allows users to design physics object prefabs
/// by drawing shapes on a grid, then saving them to JSON for later use.
/// </summary>
public class PrefabDesignerGame : IGame
{
    private GameEngine _engine = null!;
    private UiManager _uiManager = new();
    private DesignerRenderer _designerRenderer = null!;

    // Grid settings
    private const int GRID_SIZE = 10;
    private const int TOOLBAR_HEIGHT = 70;

    // Drawing/interaction modes
    private enum DrawMode { None, Polygon, Circle, Rectangle, Weld, Axis, Select }
    private DrawMode _currentMode = DrawMode.None;

    // Drawing state
    private readonly List<Vector2> _currentPolygonPoints = new();
    private Vector2? _drawStartPoint;
    private Vector2 _currentMousePos;
    private bool _isDrawing;

    // Completed shapes (design-time representation)
    private readonly List<PrefabShape> _shapes = new();

    // Constraints between shapes
    private readonly List<PrefabConstraint> _constraints = new();

    // Constraint creation state
    private int? _firstSelectedShapeIndex;
    private Vector2? _firstAnchorPoint;

    // Shape selection state (for deletion)
    private int? _selectedShapeIndex;

    // View panning state
    private bool _isPanning = false;
    private Vector2 _panStartScreenPos;

    public void Initialize(GameEngine engine)
    {
        _engine = engine;
        _designerRenderer = new DesignerRenderer(engine.WindowWidth, TOOLBAR_HEIGHT);

        // Hide debug UI
        _engine.Renderer.ShowDebugUI = false;

        // Pause physics simulation
        _engine.PhysicsSystem.IsPaused = true;
        _engine.PhysicsSystem.Gravity = Vector2.Zero;

        CreateToolbar();

        Console.WriteLine("Prefab Designer loaded!");
        Console.WriteLine("  S - Select mode | Delete/Backspace - Remove selected shape");
        Console.WriteLine("  P - Polygon mode | C - Circle | R - Rectangle");
        Console.WriteLine("  W - Weld (rigid) | A - Axis (rotating joint)");
        Console.WriteLine("  For constraints: click first shape, then second shape");
        Console.WriteLine("  ESC - Return to menu");
    }

    #region Toolbar Setup

    private void CreateToolbar()
    {
        var font = _engine.Renderer.DefaultFont;
        float centerX = _engine.WindowWidth / 2f;
        float buttonWidth = 45f;
        float buttonHeight = 35f;
        float spacing = 6f;
        float toolbarY = 5f;

        // Calculate starting X to center the toolbar (10 buttons now)
        int numButtons = 10;
        float totalWidth = (buttonWidth * numButtons) + (spacing * (numButtons - 1));
        float startX = centerX - totalWidth / 2f;
        int btnIdx = 0;

        // Select button (S) - first for easy access
        AddToolbarButton("S", startX, ref btnIdx, buttonWidth, buttonHeight, spacing, toolbarY, font, 
            () => SetDrawMode(DrawMode.Select));

        // Polygon button (P)
        AddToolbarButton("P", startX, ref btnIdx, buttonWidth, buttonHeight, spacing, toolbarY, font,
            () => SetDrawMode(DrawMode.Polygon));

        // Circle button (C)
        AddToolbarButton("C", startX, ref btnIdx, buttonWidth, buttonHeight, spacing, toolbarY, font,
            () => SetDrawMode(DrawMode.Circle));

        // Rectangle button (R)
        AddToolbarButton("R", startX, ref btnIdx, buttonWidth, buttonHeight, spacing, toolbarY, font,
            () => SetDrawMode(DrawMode.Rectangle));

        // Separator space
        btnIdx++;

        // Weld button (W) - rigid connection
        AddToolbarButton("W", startX, ref btnIdx, buttonWidth, buttonHeight, spacing, toolbarY, font,
            () => SetDrawMode(DrawMode.Weld));

        // Axis button (A) - rotating joint
        AddToolbarButton("A", startX, ref btnIdx, buttonWidth, buttonHeight, spacing, toolbarY, font,
            () => SetDrawMode(DrawMode.Axis));

        // Separator space
        btnIdx++;

        // Load button
        AddToolbarButton("Load", startX, ref btnIdx, buttonWidth, buttonHeight, spacing, toolbarY, font,
            LoadPrefabDialog);

        // Save button
        AddToolbarButton("Save", startX, ref btnIdx, buttonWidth, buttonHeight, spacing, toolbarY, font,
            SavePrefab);

        // Clear button
        AddToolbarButton("Clr", startX, ref btnIdx, buttonWidth, buttonHeight, spacing, toolbarY, font,
            ClearAll);

        // Second row for status/hints
        var hintLabel = new UiTextLabel("Select a tool to begin", font)
        {
            Position = new Vector2(10, 45),
            CharacterSize = 12
        };
        _uiManager.Add(hintLabel);
    }

    private void AddToolbarButton(string label, float startX, ref int btnIdx, float buttonWidth, 
        float buttonHeight, float spacing, float toolbarY, Font font, Action onClick)
    {
        var button = new UiButton(label, font,
            new Vector2(startX + (buttonWidth + spacing) * btnIdx++, toolbarY),
            new Vector2(buttonWidth, buttonHeight));
        button.OnClick += _ => onClick();
        _uiManager.Add(button);
    }

    private void SetDrawMode(DrawMode mode)
    {
        CancelCurrentDrawing();
        _currentMode = mode;

        string modeDesc = mode switch
        {
            DrawMode.Polygon => "Polygon: Click to add points, Enter or click start to close",
            DrawMode.Circle => "Circle: Click and drag to draw",
            DrawMode.Rectangle => "Rectangle: Click and drag to draw",
            DrawMode.Weld => "Weld: Click first shape, then second shape (rigid connection)",
            DrawMode.Axis => "Axis: Click first shape, then second shape (rotating joint)",
            DrawMode.Select => "Select: Click shape to select, Delete/Backspace to remove",
            _ => "Select a tool"
        };
        Console.WriteLine($"Mode: {modeDesc}");
    }

    #endregion

    #region State Management

    private void CancelCurrentDrawing()
    {
        _currentPolygonPoints.Clear();
        _drawStartPoint = null;
        _isDrawing = false;
        _firstSelectedShapeIndex = null;
        _firstAnchorPoint = null;
    }

    private void ClearAll()
    {
        _shapes.Clear();
        _constraints.Clear();
        _selectedShapeIndex = null;
        CancelCurrentDrawing();
        Console.WriteLine("Cleared all shapes and constraints");
    }

    #endregion

    #region Update Loop

    public void Update(float deltaTime, InputManager inputManager)
    {
        _currentMousePos = inputManager.MousePosition;

        if (HandleEscapeKey(inputManager)) return;
        if (HandleDeleteKey(inputManager)) return;

        HandleViewPanning(inputManager);
        HandleZoom(inputManager);

        if (HandlePolygonClose(inputManager)) return;

        HandleMouseClick(inputManager);
        HandleMouseRelease(inputManager);
        UpdateDrawingState(inputManager);
    }

    private bool HandleEscapeKey(InputManager inputManager)
    {
        if (inputManager.IsKeyPressedBuffered(Keyboard.Key.Escape))
        {
            _engine.SwitchGame(new MenuGame());
            inputManager.ConsumeKeyPress(Keyboard.Key.Escape);
            return true;
        }
        return false;
    }

    private bool HandleDeleteKey(InputManager inputManager)
    {
        if (_selectedShapeIndex.HasValue &&
            (inputManager.IsKeyPressedBuffered(Keyboard.Key.Delete) || 
             inputManager.IsKeyPressedBuffered(Keyboard.Key.Backspace)))
        {
            DeleteSelectedShape();
            inputManager.ConsumeKeyPress(Keyboard.Key.Delete);
            inputManager.ConsumeKeyPress(Keyboard.Key.Backspace);
            return true;
        }
        return false;
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

    private void HandleZoom(InputManager inputManager)
    {
        if (Math.Abs(inputManager.ScrollWheelDelta) > 1e-6f)
        {
            _engine.Renderer.ZoomView(inputManager.ScrollWheelDelta, inputManager.MouseScreenPosition);
        }
    }

    private bool HandlePolygonClose(InputManager inputManager)
    {
        if (inputManager.IsKeyPressedBuffered(Keyboard.Key.Enter) && 
            _currentMode == DrawMode.Polygon && 
            _currentPolygonPoints.Count >= 3)
        {
            FinishPolygon();
            inputManager.ConsumeKeyPress(Keyboard.Key.Enter);
            return true;
        }
        return false;
    }

    private void HandleMouseClick(InputManager inputManager)
    {
        if (!inputManager.IsMousePressed(Mouse.Button.Left)) return;

        // Check toolbar first (screen coordinates)
        if (inputManager.MouseScreenPosition.Y < TOOLBAR_HEIGHT)
        {
            _uiManager.HandleClick(inputManager.MouseScreenPosition);
            return;
        }

        // Handle drawing/selection based on current mode (world coordinates)
        HandleDrawingClick(_currentMousePos);
    }

    private void HandleMouseRelease(InputManager inputManager)
    {
        if (!inputManager.IsMouseHeld(Mouse.Button.Left) && _isDrawing &&
            (_currentMode == DrawMode.Circle || _currentMode == DrawMode.Rectangle))
        {
            FinishShape();
        }
    }

    private void UpdateDrawingState(InputManager inputManager)
    {
        if (inputManager.IsMouseHeld(Mouse.Button.Left) && _drawStartPoint.HasValue)
        {
            _isDrawing = true;
        }
    }

    #endregion

    #region Click Handlers

    private void HandleDrawingClick(Vector2 mousePos)
    {
        Vector2 snappedPos = ShapeGeometry.SnapToGrid(mousePos, GRID_SIZE, TOOLBAR_HEIGHT);

        switch (_currentMode)
        {
            case DrawMode.Polygon:
                HandlePolygonClick(snappedPos);
                break;

            case DrawMode.Circle:
            case DrawMode.Rectangle:
                _drawStartPoint = snappedPos;
                _isDrawing = true;
                break;

            case DrawMode.Weld:
            case DrawMode.Axis:
                HandleConstraintClick(mousePos, snappedPos);
                break;

            case DrawMode.Select:
            case DrawMode.None:
                HandleSelectionClick(mousePos);
                break;
        }
    }

    private void HandleSelectionClick(Vector2 worldPos)
    {
        int? clickedIndex = ShapeGeometry.FindShapeAtPoint(worldPos, _shapes);

        if (clickedIndex.HasValue)
        {
            _selectedShapeIndex = clickedIndex;
            Console.WriteLine($"Selected shape [{clickedIndex.Value}] - Press Delete or Backspace to remove");
        }
        else
        {
            _selectedShapeIndex = null;
        }
    }

    private void HandlePolygonClick(Vector2 snappedPos)
    {
        // Check if clicking on the first point to close the polygon
        if (_currentPolygonPoints.Count >= 3)
        {
            float distToFirst = Vector2.Distance(snappedPos, _currentPolygonPoints[0]);
            if (distToFirst < GRID_SIZE)
            {
                FinishPolygon();
                return;
            }
        }

        _currentPolygonPoints.Add(snappedPos);
    }

    private void HandleConstraintClick(Vector2 worldPos, Vector2 snappedPos)
    {
        int? clickedIndex = ShapeGeometry.FindShapeAtPoint(worldPos, _shapes);

        if (!clickedIndex.HasValue)
        {
            Console.WriteLine("No shape at click location");
            return;
        }

        if (!_firstSelectedShapeIndex.HasValue)
        {
            // First click - select first shape and anchor point
            _firstSelectedShapeIndex = clickedIndex;
            _firstAnchorPoint = snappedPos;

            string hint = _currentMode == DrawMode.Axis
                ? $"Selected BASE shape [{clickedIndex.Value}] (cyan) at pivot - now click the ROTATING shape"
                : $"Selected shape [{clickedIndex.Value}] - now click second shape at the weld point";
            Console.WriteLine(hint);
        }
        else
        {
            // Second click - create constraint
            if (clickedIndex.Value == _firstSelectedShapeIndex.Value)
            {
                Console.WriteLine("Cannot connect shape to itself. Select a different shape.");
                return;
            }

            CreateConstraint(clickedIndex.Value);
        }
    }

    private void CreateConstraint(int secondShapeIndex)
    {
        var constraintType = _currentMode == DrawMode.Weld ? ConstraintType.Weld : ConstraintType.Axis;

        var constraint = new PrefabConstraint
        {
            Type = constraintType,
            ShapeIndexA = _firstSelectedShapeIndex!.Value,
            ShapeIndexB = secondShapeIndex,
            AnchorA = _firstAnchorPoint!.Value,
            AnchorB = _firstAnchorPoint!.Value
        };
        _constraints.Add(constraint);

        string description = constraintType == ConstraintType.Axis
            ? $"Created Axis: [{_firstSelectedShapeIndex.Value}] (base/cyan) <-> [{secondShapeIndex}] (rotating/green) at pivot {_firstAnchorPoint!.Value}"
            : $"Created Weld: [{_firstSelectedShapeIndex.Value}] <-> [{secondShapeIndex}] at {_firstAnchorPoint!.Value}";
        Console.WriteLine(description);

        _firstSelectedShapeIndex = null;
        _firstAnchorPoint = null;
    }

    #endregion

    #region Shape Creation

    private void FinishPolygon()
    {
        if (_currentPolygonPoints.Count >= 3)
        {
            var shape = new PrefabShape
            {
                Type = ShapeType.Polygon,
                Points = _currentPolygonPoints.ToArray()
            };
            _shapes.Add(shape);
            Console.WriteLine($"Created polygon with {_currentPolygonPoints.Count} vertices");
        }

        _currentPolygonPoints.Clear();
    }

    private void FinishShape()
    {
        if (!_drawStartPoint.HasValue) return;

        Vector2 startPoint = _drawStartPoint.Value;
        Vector2 endPoint = ShapeGeometry.SnapToGrid(_currentMousePos, GRID_SIZE, TOOLBAR_HEIGHT);

        if (_currentMode == DrawMode.Circle)
        {
            CreateCircle(startPoint, endPoint);
        }
        else if (_currentMode == DrawMode.Rectangle)
        {
            CreateRectangle(startPoint, endPoint);
        }

        _drawStartPoint = null;
        _isDrawing = false;
    }

    private void CreateCircle(Vector2 startPoint, Vector2 endPoint)
    {
        float size = Math.Max(Math.Abs(endPoint.X - startPoint.X), Math.Abs(endPoint.Y - startPoint.Y));
        if (size > GRID_SIZE)
        {
            var shape = new PrefabShape
            {
                Type = ShapeType.Circle,
                Center = startPoint + new Vector2(size / 2, size / 2),
                Radius = size / 2
            };
            _shapes.Add(shape);
            Console.WriteLine($"Created circle with radius {shape.Radius}");
        }
    }

    private void CreateRectangle(Vector2 startPoint, Vector2 endPoint)
    {
        float width = Math.Abs(endPoint.X - startPoint.X);
        float height = Math.Abs(endPoint.Y - startPoint.Y);

        if (width > 0 && height > 0)
        {
            Vector2 topLeft = new(
                Math.Min(startPoint.X, endPoint.X),
                Math.Min(startPoint.Y, endPoint.Y)
            );

            var shape = new PrefabShape
            {
                Type = ShapeType.Rectangle,
                Position = topLeft,
                Width = width,
                Height = height
            };
            _shapes.Add(shape);
            Console.WriteLine($"Created rectangle {width}x{height}");
        }
    }

    #endregion

    #region Shape Deletion

    private void DeleteSelectedShape()
    {
        if (!_selectedShapeIndex.HasValue || _selectedShapeIndex.Value >= _shapes.Count)
            return;

        int indexToRemove = _selectedShapeIndex.Value;

        // Remove constraints that reference this shape
        _constraints.RemoveAll(c => c.ShapeIndexA == indexToRemove || c.ShapeIndexB == indexToRemove);

        // Update constraint indices for shapes after the removed one
        foreach (var constraint in _constraints)
        {
            if (constraint.ShapeIndexA > indexToRemove)
                constraint.ShapeIndexA--;
            if (constraint.ShapeIndexB > indexToRemove)
                constraint.ShapeIndexB--;
        }

        // Remove the shape
        _shapes.RemoveAt(indexToRemove);
        Console.WriteLine($"Deleted shape [{indexToRemove}] and its constraints");

        // Clear selection
        _selectedShapeIndex = null;

        // Update constraint creation state if needed
        if (_firstSelectedShapeIndex.HasValue)
        {
            if (_firstSelectedShapeIndex.Value == indexToRemove)
            {
                _firstSelectedShapeIndex = null;
                _firstAnchorPoint = null;
            }
            else if (_firstSelectedShapeIndex.Value > indexToRemove)
            {
                _firstSelectedShapeIndex = _firstSelectedShapeIndex.Value - 1;
            }
        }
    }

    #endregion

    #region File Operations

    private void SavePrefab()
    {
        PrefabFileManager.SavePrefab(_shapes, _constraints);
    }

    private void LoadPrefabDialog()
    {
        var files = PrefabFileManager.ListAvailablePrefabs();
        if (files.Length == 0)
        {
            Console.WriteLine("No Prefabs directory found. Create some prefabs first!");
            return;
        }

        Console.WriteLine("Type the number of the prefab to load, or press ESC to cancel.");

        // Load the most recent prefab automatically for now
        var mostRecent = PrefabFileManager.GetMostRecentPrefab();
        if (mostRecent != null)
        {
            LoadPrefab(mostRecent);
        }
    }

    private void LoadPrefab(string filePath)
    {
        var prefab = PrefabFileManager.LoadPrefab(filePath);
        if (prefab == null) return;

        // Clear current state
        _shapes.Clear();
        _constraints.Clear();
        _selectedShapeIndex = null;
        CancelCurrentDrawing();

        // Load shapes
        _shapes.AddRange(prefab.Shapes);

        // Load constraints
        if (prefab.Constraints != null)
        {
            _constraints.AddRange(prefab.Constraints);
        }
    }

    #endregion

    #region Rendering

    public void Render(Renderer renderer)
    {
        renderer.Window.SetView(renderer.GameView);

        // Draw grid
        _designerRenderer.DrawGrid(renderer, _engine.WindowWidth, _engine.WindowHeight, GRID_SIZE, TOOLBAR_HEIGHT);

        // Draw shapes
        _designerRenderer.DrawShapes(renderer, _shapes);

        // Draw constraints
        _designerRenderer.DrawConstraints(renderer, _constraints, _shapes);

        // Draw current drawing preview
        DrawCurrentDrawing(renderer);

        // Draw constraint creation highlight
        DrawConstraintCreationPreview(renderer);

        // Draw selection highlight
        if (_selectedShapeIndex.HasValue && _selectedShapeIndex.Value < _shapes.Count)
        {
            _designerRenderer.DrawShapeHighlight(renderer, _shapes[_selectedShapeIndex.Value], 
                new Color(255, 100, 255, 180));
        }

        // Draw toolbar
        _designerRenderer.DrawToolbarBackground(renderer);
        _uiManager.Draw(renderer.Window);

        // Draw status text
        DrawStatusText(renderer);
    }

    private void DrawCurrentDrawing(Renderer renderer)
    {
        renderer.Window.SetView(renderer.GameView);

        Vector2 snappedMouse = ShapeGeometry.SnapToGrid(_currentMousePos, GRID_SIZE, TOOLBAR_HEIGHT);
        _designerRenderer.DrawCursorIndicator(renderer, snappedMouse);

        switch (_currentMode)
        {
            case DrawMode.Polygon:
                _designerRenderer.DrawPolygonPreview(renderer, _currentPolygonPoints, snappedMouse, GRID_SIZE);
                break;

            case DrawMode.Circle when _drawStartPoint.HasValue && _isDrawing:
                _designerRenderer.DrawCirclePreview(renderer, _drawStartPoint.Value, snappedMouse);
                break;

            case DrawMode.Rectangle when _drawStartPoint.HasValue && _isDrawing:
                _designerRenderer.DrawRectanglePreview(renderer, _drawStartPoint.Value, snappedMouse);
                break;
        }
    }

    private void DrawConstraintCreationPreview(Renderer renderer)
    {
        if (!_firstSelectedShapeIndex.HasValue || _firstSelectedShapeIndex.Value >= _shapes.Count)
            return;

        Color highlightColor = _currentMode == DrawMode.Axis
            ? new Color(100, 200, 255, 150)  // Cyan for axis base
            : new Color(255, 100, 100, 150); // Red for weld

        _designerRenderer.DrawShapeHighlight(renderer, _shapes[_firstSelectedShapeIndex.Value], highlightColor);

        if (_firstAnchorPoint.HasValue)
        {
            Color anchorColor = _currentMode == DrawMode.Axis
                ? new Color(255, 255, 100, 255)  // Yellow pivot
                : new Color(255, 150, 150, 255); // Light red weld point

            _designerRenderer.DrawCircle(renderer, _firstAnchorPoint.Value, 8f, anchorColor, Color.White, 2f);

            Vector2 center = ShapeGeometry.GetShapeCenter(_shapes[_firstSelectedShapeIndex.Value]);
            _designerRenderer.DrawLine(renderer, center, _firstAnchorPoint.Value, highlightColor, 2f);
        }
    }

    private void DrawStatusText(Renderer renderer)
    {
        string modeText = _currentMode switch
        {
            DrawMode.Polygon => "Mode: Polygon (click to add points, Enter/click start to close)",
            DrawMode.Circle => "Mode: Circle (click and drag)",
            DrawMode.Rectangle => "Mode: Rectangle (click and drag)",
            DrawMode.Weld => _firstSelectedShapeIndex.HasValue
                ? "Mode: Weld - Click second shape"
                : "Mode: Weld - Click first shape",
            DrawMode.Axis => _firstSelectedShapeIndex.HasValue
                ? "Mode: Axis - Click second shape"
                : "Mode: Axis - Click first shape",
            DrawMode.Select => _selectedShapeIndex.HasValue
                ? $"Mode: Select - Shape [{_selectedShapeIndex.Value}] selected (Del to remove)"
                : "Mode: Select - Click a shape to select",
            _ => "Select a tool from the toolbar (S/P/C/R for shapes, W/A for constraints)"
        };
        renderer.DrawText(modeText, 10, _engine.WindowHeight - 30, 14, Color.White);

        renderer.DrawText($"Shapes: {_shapes.Count} | Constraints: {_constraints.Count}",
            _engine.WindowWidth - 200, _engine.WindowHeight - 30, 14, Color.White);
    }

    #endregion

    public void Shutdown()
    {
        _uiManager.Clear();
        _shapes.Clear();
        _constraints.Clear();
        _currentPolygonPoints.Clear();
        _designerRenderer.Dispose();

        // Unpause physics for other games
        _engine.PhysicsSystem.IsPaused = false;
    }
}
