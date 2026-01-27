#nullable enable
using System.Numerics;
using System.Text.Json;
using physics.Engine;
using physics.Engine.Core;
using physics.Engine.Helpers;
using physics.Engine.Input;
using physics.Engine.Objects;
using physics.Engine.Rendering;
using physics.Engine.Rendering.UI;
using physics.Engine.Classes.ObjectTemplates;
using SharpPhysics.Demo.Helpers;
using SFML.Graphics;
using SFML.System;
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
    private ObjectTemplates _objectTemplates = null!;

    // Grid settings
    private const int GRID_SIZE = 10;
    private const int TOOLBAR_HEIGHT = 70;
    private Color _gridColor = new(60, 60, 70);
    private Color _gridMajorColor = new(80, 80, 100);

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

    // Cached SFML shapes to avoid GC pressure
    private readonly RectangleShape _cachedLine = new();
    private readonly CircleShape _cachedCircle = new();
    private readonly RectangleShape _cachedRect = new();
    private readonly ConvexShape _cachedConvex = new();
    private readonly RectangleShape _cachedToolbarBg = new();

    public void Initialize(GameEngine engine)
    {
        _engine = engine;
        _objectTemplates = new ObjectTemplates(engine.PhysicsSystem);

        // Hide debug UI
        _engine.Renderer.ShowDebugUI = false;

        // Pause physics simulation
        _engine.PhysicsSystem.IsPaused = true;
        _engine.PhysicsSystem.Gravity = Vector2.Zero;

        // Initialize cached toolbar background
        _cachedToolbarBg.Size = new Vector2f(_engine.WindowWidth, TOOLBAR_HEIGHT);
        _cachedToolbarBg.FillColor = new Color(30, 30, 40);

        CreateToolbar();

        Console.WriteLine("Prefab Designer loaded!");
            Console.WriteLine("  P - Polygon mode | C - Circle | R - Rectangle");
            Console.WriteLine("  W - Weld (rigid) | A - Axis (rotating joint)");
            Console.WriteLine("  S - Select mode | Delete/Backspace - Remove selected shape");
            Console.WriteLine("  For constraints: click first shape, then second shape");
            Console.WriteLine("  ESC - Return to menu");
        }

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
            var selectButton = new UiButton("S", font,
                new Vector2(startX + (buttonWidth + spacing) * btnIdx++, toolbarY),
                new Vector2(buttonWidth, buttonHeight));
            selectButton.OnClick += _ => SetDrawMode(DrawMode.Select);
            _uiManager.Add(selectButton);

            // Polygon button (P)
            var polygonButton = new UiButton("P", font,
                new Vector2(startX + (buttonWidth + spacing) * btnIdx++, toolbarY),
                new Vector2(buttonWidth, buttonHeight));
            polygonButton.OnClick += _ => SetDrawMode(DrawMode.Polygon);
            _uiManager.Add(polygonButton);

            // Circle button (C)
            var circleButton = new UiButton("C", font,
                new Vector2(startX + (buttonWidth + spacing) * btnIdx++, toolbarY),
                new Vector2(buttonWidth, buttonHeight));
            circleButton.OnClick += _ => SetDrawMode(DrawMode.Circle);
            _uiManager.Add(circleButton);

            // Rectangle button (R)
            var rectangleButton = new UiButton("R", font,
                new Vector2(startX + (buttonWidth + spacing) * btnIdx++, toolbarY),
                new Vector2(buttonWidth, buttonHeight));
            rectangleButton.OnClick += _ => SetDrawMode(DrawMode.Rectangle);
            _uiManager.Add(rectangleButton);

            // Separator space
        btnIdx++; // Skip one slot for visual separation

        // Weld button (W) - rigid connection
        var weldButton = new UiButton("W", font,
            new Vector2(startX + (buttonWidth + spacing) * btnIdx++, toolbarY),
            new Vector2(buttonWidth, buttonHeight));
        weldButton.OnClick += _ => SetDrawMode(DrawMode.Weld);
        _uiManager.Add(weldButton);

        // Axis button (A) - rotating joint
        var axisButton = new UiButton("A", font,
            new Vector2(startX + (buttonWidth + spacing) * btnIdx++, toolbarY),
            new Vector2(buttonWidth, buttonHeight));
        axisButton.OnClick += _ => SetDrawMode(DrawMode.Axis);
        _uiManager.Add(axisButton);

        // Separator space
        btnIdx++;

        // Load button
        var loadButton = new UiButton("Load", font,
            new Vector2(startX + (buttonWidth + spacing) * btnIdx++, toolbarY),
            new Vector2(buttonWidth, buttonHeight));
        loadButton.OnClick += _ => LoadPrefabDialog();
        _uiManager.Add(loadButton);

        // Save button
        var saveButton = new UiButton("Save", font,
            new Vector2(startX + (buttonWidth + spacing) * btnIdx++, toolbarY),
            new Vector2(buttonWidth, buttonHeight));
        saveButton.OnClick += _ => SavePrefab();
        _uiManager.Add(saveButton);

        // Clear button
        var clearButton = new UiButton("Clr", font,
            new Vector2(startX + (buttonWidth + spacing) * btnIdx++, toolbarY),
            new Vector2(buttonWidth, buttonHeight));
        clearButton.OnClick += _ => ClearAll();
        _uiManager.Add(clearButton);

        // Second row for status/hints
        var hintLabel = new UiTextLabel("Select a tool to begin", font)
        {
            Position = new Vector2(10, 45),
            CharacterSize = 12
        };
        _uiManager.Add(hintLabel);
    }

    private void SetDrawMode(DrawMode mode)
    {
        // Cancel any in-progress drawing/selection
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

            private void CancelCurrentDrawing()
            {
                _currentPolygonPoints.Clear();
                _drawStartPoint = null;
                _isDrawing = false;
                _firstSelectedShapeIndex = null;
                _firstAnchorPoint = null;
                // Don't clear _selectedShapeIndex here - keep selection persistent
                    }

                    private void ClearAll()
                    {
                        _shapes.Clear();
                        _constraints.Clear();
                _selectedShapeIndex = null;
                CancelCurrentDrawing();
                Console.WriteLine("Cleared all shapes and constraints");
            }

            /// <summary>
            /// Snaps a point to the grid, accounting for toolbar offset.
            /// Grid lines start at Y=TOOLBAR_HEIGHT, so we offset before snapping.
            /// </summary>
            private Vector2 SnapToGrid(Vector2 point)
            {
                // For X: snap normally since grid starts at X=0
                float snappedX = MathF.Round(point.X / GRID_SIZE) * GRID_SIZE;

        // For Y: account for the toolbar offset
        // Grid starts at Y=TOOLBAR_HEIGHT, so subtract offset, snap, then add back
        float adjustedY = point.Y - TOOLBAR_HEIGHT;
        float snappedY = MathF.Round(adjustedY / GRID_SIZE) * GRID_SIZE + TOOLBAR_HEIGHT;

        return new Vector2(snappedX, snappedY);
    }

    public void Update(float deltaTime, InputManager inputManager)
    {
        // keyState.MousePosition is in world coordinates (for game logic/drawing)
        // keyState.MouseScreenPosition is in screen coordinates (for UI hit detection)
        _currentMousePos = inputManager.MousePosition;
        // Check for ESC to return to menu
        if (inputManager.IsKeyPressedBuffered(Keyboard.Key.Escape))
        {
            _engine.SwitchGame(new MenuGame());
            inputManager.ConsumeKeyPress(Keyboard.Key.Escape);
            return;
        }

        // Handle Delete/Backspace to remove selected shape
        if (_selectedShapeIndex.HasValue &&
            (inputManager.IsKeyPressedBuffered(Keyboard.Key.Delete) || inputManager.IsKeyPressedBuffered(Keyboard.Key.Backspace)))
        {
            DeleteSelectedShape();
            inputManager.ConsumeKeyPress(Keyboard.Key.Delete);
            inputManager.ConsumeKeyPress(Keyboard.Key.Backspace);
            return;
        }

        // Handle view panning with middle mouse button
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

        // Handle scroll wheel zoom
        if (Math.Abs(inputManager.ScrollWheelDelta) > 1e-6f)
        {
            _engine.Renderer.ZoomView(inputManager.ScrollWheelDelta, inputManager.MouseScreenPosition);
        }

        // Handle Enter key for closing polygon
        if (inputManager.IsKeyPressedBuffered(Keyboard.Key.Enter) && _currentMode == DrawMode.Polygon && _currentPolygonPoints.Count >= 3)
        {
            FinishPolygon();
            inputManager.ConsumeKeyPress(Keyboard.Key.Enter);
            return;
        }

        // Handle keyboard shortcuts for tool selection
        if (inputManager.IsKeyPressedBuffered(Keyboard.Key.Escape))
        {
            inputManager.ConsumeKeyPress(Keyboard.Key.Escape);
            return; // Already handled above
        }

        // Handle UI clicks first (toolbar is in screen coordinates)
        if (inputManager.IsMousePressed(Mouse.Button.Left))
        {
            // Use screen position for toolbar detection
            if (inputManager.MouseScreenPosition.Y < TOOLBAR_HEIGHT)
            {
                _uiManager.HandleClick(inputManager.MouseScreenPosition);
                return;
            }

            // Handle drawing/selection based on current mode (use world position)
            HandleDrawingClick(_currentMousePos);
        }

            // Handle mouse release for circle/rectangle drawing
            if (!inputManager.IsMouseHeld(Mouse.Button.Left) && _isDrawing &&
                (_currentMode == DrawMode.Circle || _currentMode == DrawMode.Rectangle))
            {
                FinishShape();
            }

            // Update drawing state
            if (inputManager.IsMouseHeld(Mouse.Button.Left) && _drawStartPoint.HasValue)
            {
                _isDrawing = true;
            }
        }

        private void HandleDrawingClick(Vector2 mousePos)
        {
            Vector2 snappedPos = SnapToGrid(mousePos);

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
                    HandleSelectionClick(mousePos);
                    break;

                case DrawMode.None:
                    // Allow selection even in None mode for convenience
                    HandleSelectionClick(mousePos);
                    break;
            }
        }

        private void HandleSelectionClick(Vector2 worldPos)
        {
            int? clickedIndex = FindShapeAtPoint(worldPos);

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

            // Also clear constraint creation state if it referenced this shape
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

        private void HandleConstraintClick(Vector2 worldPos, Vector2 snappedPos)
        {
            // Find which shape was clicked
            int? clickedIndex = FindShapeAtPoint(worldPos);

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

            var constraintType = _currentMode == DrawMode.Weld ? ConstraintType.Weld : ConstraintType.Axis;

            // For BOTH Weld and Axis constraints: Both anchors store the SAME world point
            // This ensures:
            // - Weld: Objects stay exactly where they are (no pulling together)
            // - Axis: Objects rotate around the same pivot point
            // The first click position defines the connection point
            Vector2 anchorB = _firstAnchorPoint!.Value;

            var constraint = new PrefabConstraint
            {
                Type = constraintType,
                ShapeIndexA = _firstSelectedShapeIndex.Value,
                ShapeIndexB = clickedIndex.Value,
                AnchorA = _firstAnchorPoint!.Value,
                AnchorB = anchorB
            };
            _constraints.Add(constraint);

            string description = constraintType == ConstraintType.Axis
                ? $"Created Axis: [{_firstSelectedShapeIndex.Value}] (base/cyan) <-> [{clickedIndex.Value}] (rotating/green) at pivot {_firstAnchorPoint!.Value}"
                : $"Created Weld: [{_firstSelectedShapeIndex.Value}] <-> [{clickedIndex.Value}] at {_firstAnchorPoint!.Value}";
            Console.WriteLine(description);

            // Reset selection state
            _firstSelectedShapeIndex = null;
            _firstAnchorPoint = null;
        }
    }

    /// <summary>
    /// Gets the center point of a shape for constraint anchoring.
    /// </summary>
    private Vector2 GetShapeCenter(PrefabShape shape)
    {
        switch (shape.Type)
        {
            case ShapeType.Circle:
                return shape.Center;
            case ShapeType.Rectangle:
                return shape.Position + new Vector2(shape.Width / 2, shape.Height / 2);
            case ShapeType.Polygon:
                if (shape.Points == null || shape.Points.Length == 0)
                    return Vector2.Zero;
                // Calculate centroid
                Vector2 sum = Vector2.Zero;
                foreach (var pt in shape.Points)
                    sum += pt;
                return sum / shape.Points.Length;
            default:
                return Vector2.Zero;
        }
    }

    private int? FindShapeAtPoint(Vector2 point)
    {
        // Check shapes in reverse order (top-most first)
        for (int i = _shapes.Count - 1; i >= 0; i--)
        {
            if (IsPointInShape(point, _shapes[i]))
            {
                return i;
            }
        }
        return null;
    }

    private bool IsPointInShape(Vector2 point, PrefabShape shape)
    {
        switch (shape.Type)
        {
            case ShapeType.Circle:
                float dist = Vector2.Distance(point, shape.Center);
                return dist <= shape.Radius;

            case ShapeType.Rectangle:
                return point.X >= shape.Position.X &&
                       point.X <= shape.Position.X + shape.Width &&
                       point.Y >= shape.Position.Y &&
                       point.Y <= shape.Position.Y + shape.Height;

            case ShapeType.Polygon:
                if (shape.Points == null || shape.Points.Length < 3) return false;
                return IsPointInPolygon(point, shape.Points);
        }
        return false;
    }

    private bool IsPointInPolygon(Vector2 point, Vector2[] polygon)
    {
        int crossings = 0;
        int n = polygon.Length;

        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            Vector2 vi = polygon[i];
            Vector2 vj = polygon[j];

            if ((vi.Y <= point.Y && vj.Y > point.Y) || (vj.Y <= point.Y && vi.Y > point.Y))
            {
                float t = (point.Y - vi.Y) / (vj.Y - vi.Y);
                float xIntersect = vi.X + t * (vj.X - vi.X);

                if (point.X < xIntersect)
                    crossings++;
            }
        }

        return (crossings % 2) == 1;
    }

    private void HandlePolygonClick(Vector2 snappedPos)
    {
        // Check if clicking on the first point to close the polygon
        if (_currentPolygonPoints.Count >= 3)
        {
            Vector2 firstPoint = _currentPolygonPoints[0];
            float distToFirst = Vector2.Distance(snappedPos, firstPoint);

            if (distToFirst < GRID_SIZE)
            {
                FinishPolygon();
                return;
            }
        }

        // Add new point
        _currentPolygonPoints.Add(snappedPos);
    }

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
        Vector2 endPoint = SnapToGrid(_currentMousePos);

        if (_currentMode == DrawMode.Circle)
        {
            // Create circle with 1:1 aspect ratio
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
        else if (_currentMode == DrawMode.Rectangle)
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

        _drawStartPoint = null;
        _isDrawing = false;
    }

    private void SavePrefab()
    {
        if (_shapes.Count == 0)
        {
            Console.WriteLine("No shapes to save!");
            return;
        }

        var prefab = new PrefabData
        {
            Name = $"Prefab_{DateTime.Now:yyyyMMdd_HHmmss}",
            Shapes = _shapes.ToArray(),
            Constraints = _constraints.Count > 0 ? _constraints.ToArray() : null
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new Vector2JsonConverter() }
        };

        string json = JsonSerializer.Serialize(prefab, options);

        // Ensure Resources/Prefabs directory exists
        string prefabDir = Path.Combine("Resources", "Prefabs");
        Directory.CreateDirectory(prefabDir);

        string filePath = Path.Combine(prefabDir, $"{prefab.Name}.json");
        File.WriteAllText(filePath, json);

        Console.WriteLine($"Saved prefab to: {filePath}");
        Console.WriteLine($"  Shapes: {_shapes.Count}, Constraints: {_constraints.Count}");
    }

    private void LoadPrefabDialog()
    {
        string prefabDir = Path.Combine("Resources", "Prefabs");

        if (!Directory.Exists(prefabDir))
        {
            Console.WriteLine("No Prefabs directory found. Create some prefabs first!");
            return;
        }

        var files = Directory.GetFiles(prefabDir, "*.json");
        if (files.Length == 0)
        {
            Console.WriteLine("No prefab files found in Resources/Prefabs/");
            return;
        }

        Console.WriteLine("Available prefabs:");
        for (int i = 0; i < files.Length; i++)
        {
            Console.WriteLine($"  {i + 1}: {Path.GetFileNameWithoutExtension(files[i])}");
        }
        Console.WriteLine("Type the number of the prefab to load, or press ESC to cancel.");

        // For now, load the most recent prefab automatically
        // A proper UI dialog would be better, but this works for the demo
        LoadPrefab(files[^1]); // Load most recent
    }

    private void LoadPrefab(string filePath)
    {
        try
        {
            string json = File.ReadAllText(filePath);

            var options = new JsonSerializerOptions
            {
                Converters = { new Vector2JsonConverter() }
            };

            var prefab = JsonSerializer.Deserialize<PrefabData>(json, options);

            if (prefab == null)
            {
                Console.WriteLine("Failed to parse prefab file.");
                return;
            }

            // Clear current state
            _shapes.Clear();
            _constraints.Clear();
            CancelCurrentDrawing();

            // Load shapes
            _shapes.AddRange(prefab.Shapes);

            // Load constraints
            if (prefab.Constraints != null)
            {
                _constraints.AddRange(prefab.Constraints);
            }

            Console.WriteLine($"Loaded prefab '{prefab.Name}'");
            Console.WriteLine($"  Shapes: {_shapes.Count}, Constraints: {_constraints.Count}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading prefab: {ex.Message}");
        }
    }

    public void Render(Renderer renderer)
    {
        // Ensure we're in game view for drawing
        renderer.Window.SetView(renderer.GameView);

        // Draw grid first (background)
        DrawGrid(renderer);

        // Draw completed shapes
        DrawShapes(renderer);

        // Draw constraints between shapes
        DrawConstraints(renderer);

        // Draw current drawing preview
        DrawCurrentDrawing(renderer);

        // Draw constraint creation preview (first selected shape highlight)
        if (_firstSelectedShapeIndex.HasValue && _firstSelectedShapeIndex.Value < _shapes.Count)
        {
            // Use cyan highlight for axis (base shape) or red for weld
            Color highlightColor = _currentMode == DrawMode.Axis
                ? new Color(100, 200, 255, 150)  // Cyan for axis base
                : new Color(255, 100, 100, 150); // Red for weld

            DrawShapeHighlight(renderer, _shapes[_firstSelectedShapeIndex.Value], highlightColor);

                if (_firstAnchorPoint.HasValue)
                {
                    // Draw anchor point indicator with same color scheme
                    Color anchorColor = _currentMode == DrawMode.Axis
                        ? new Color(255, 255, 100, 255)  // Yellow pivot
                        : new Color(255, 150, 150, 255); // Light red weld point
                    DrawCachedCircle(renderer, _firstAnchorPoint.Value, 8f, anchorColor, Color.White, 2f);

                    // Draw line from shape center to anchor
                    Vector2 center = GetShapeCenter(_shapes[_firstSelectedShapeIndex.Value]);
                    DrawCachedLine(renderer, center, _firstAnchorPoint.Value, highlightColor, 2f);
                }
            }

            // Draw selection highlight (for deletion) - magenta to distinguish from constraint selection
            if (_selectedShapeIndex.HasValue && _selectedShapeIndex.Value < _shapes.Count)
            {
                DrawShapeHighlight(renderer, _shapes[_selectedShapeIndex.Value], new Color(255, 100, 255, 180));
            }

            // Draw toolbar background using cached shape
            renderer.Window.SetView(renderer.UiView);
            _cachedToolbarBg.Position = new Vector2f(0, 0);
            renderer.Window.Draw(_cachedToolbarBg);

            // Draw UI elements
            _uiManager.Draw(renderer.Window);

            // Draw mode indicator
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

            // Draw shape/constraint count
            renderer.DrawText($"Shapes: {_shapes.Count} | Constraints: {_constraints.Count}",
                _engine.WindowWidth - 200, _engine.WindowHeight - 30, 14, Color.White);
    }

    private void DrawConstraints(Renderer renderer)
    {
        foreach (var constraint in _constraints)
        {
            if (constraint.ShapeIndexA >= _shapes.Count || constraint.ShapeIndexB >= _shapes.Count)
                continue;

            var shapeA = _shapes[constraint.ShapeIndexA];
            var shapeB = _shapes[constraint.ShapeIndexB];
            Vector2 centerA = GetShapeCenter(shapeA);
            Vector2 centerB = GetShapeCenter(shapeB);
            Vector2 pivot = constraint.AnchorA; // Both anchors are the same point (the pivot/weld point)

            Color colorA = constraint.Type == ConstraintType.Weld
                ? new Color(255, 100, 100, 200)  // Red for weld
                : new Color(100, 200, 255, 200); // Cyan for axis (object A - the anchor/base)

            Color colorB = constraint.Type == ConstraintType.Weld
                ? new Color(255, 100, 100, 200)  // Red for weld
                : new Color(100, 255, 100, 200); // Green for axis (object B - the rotating part)

            // Draw lines from each shape center to the pivot point
            DrawCachedLine(renderer, centerA, pivot, colorA, 2f);
            DrawCachedLine(renderer, centerB, pivot, colorB, 2f);

            // Draw the pivot point
            float pivotRadius = constraint.Type == ConstraintType.Weld ? 5f : 8f;
            Color pivotColor = constraint.Type == ConstraintType.Weld
                ? new Color(255, 150, 150, 255)
                : new Color(255, 255, 100, 255); // Yellow pivot for axis
            DrawCachedCircle(renderer, pivot, pivotRadius, pivotColor, Color.White, 2f);

            // For axis constraints, draw rotation indicator around pivot
            if (constraint.Type == ConstraintType.Axis)
            {
                DrawCachedCircle(renderer, pivot, 12f, new Color(0, 0, 0, 0), colorB, 2f);

                // Draw small arrow/indicator on B's line to show it rotates
                Vector2 dirB = Vector2.Normalize(centerB - pivot);
                Vector2 perpB = new Vector2(-dirB.Y, dirB.X);
                Vector2 arrowPos = pivot + dirB * 20f;
                DrawCachedLine(renderer, arrowPos, arrowPos + perpB * 8f, colorB, 2f);
                DrawCachedLine(renderer, arrowPos, arrowPos - perpB * 8f, colorB, 2f);
            }

                    // Draw shape index labels near the constraint lines (convert world to screen coords)
                    Vector2 labelWorldA = Vector2.Lerp(centerA, pivot, 0.3f);
                    Vector2 labelWorldB = Vector2.Lerp(centerB, pivot, 0.3f);
                    var screenPosA = renderer.Window.MapCoordsToPixel(new Vector2f(labelWorldA.X, labelWorldA.Y), renderer.GameView);
                    var screenPosB = renderer.Window.MapCoordsToPixel(new Vector2f(labelWorldB.X, labelWorldB.Y), renderer.GameView);
                    renderer.DrawText($"[{constraint.ShapeIndexA}]", screenPosA.X - 10, screenPosA.Y - 8, 12, colorA);
                    renderer.DrawText($"[{constraint.ShapeIndexB}]", screenPosB.X - 10, screenPosB.Y - 8, 12, colorB);

                    // Restore GameView after DrawText (which switches to UiView internally)
                    renderer.Window.SetView(renderer.GameView);
                }
            }

    private void DrawShapeHighlight(Renderer renderer, PrefabShape shape, Color highlightColor)
    {
        switch (shape.Type)
        {
            case ShapeType.Circle:
                DrawCachedCircle(renderer, shape.Center, shape.Radius + 3, highlightColor, highlightColor, 3f);
                break;
            case ShapeType.Rectangle:
                DrawCachedRectangle(renderer, shape.Position - new Vector2(3, 3),
                    new Vector2(shape.Width + 6, shape.Height + 6),
                    new Color(0, 0, 0, 0), highlightColor, 3f);
                break;
            case ShapeType.Polygon:
                if (shape.Points != null && shape.Points.Length >= 3)
                {
                    // Draw highlighted polygon outline
                    for (int i = 0; i < shape.Points.Length; i++)
                    {
                        int next = (i + 1) % shape.Points.Length;
                        DrawCachedLine(renderer, shape.Points[i], shape.Points[next], highlightColor, 3f);
                    }
                }
                break;
        }
    }

    private void DrawGrid(Renderer renderer)
    {
        renderer.Window.SetView(renderer.GameView);

        uint width = _engine.WindowWidth;
        uint height = _engine.WindowHeight;

        // Draw vertical lines using cached shape
        for (int x = 0; x <= width; x += GRID_SIZE)
        {
            Color color = (x % (GRID_SIZE * 5) == 0) ? _gridMajorColor : _gridColor;
            DrawCachedLine(renderer, new Vector2(x, TOOLBAR_HEIGHT), new Vector2(x, height), color, 1f);
        }

        // Draw horizontal lines - start at TOOLBAR_HEIGHT and align to grid
        for (int y = TOOLBAR_HEIGHT; y <= height; y += GRID_SIZE)
        {
            Color color = ((y - TOOLBAR_HEIGHT) % (GRID_SIZE * 5) == 0) ? _gridMajorColor : _gridColor;
            DrawCachedLine(renderer, new Vector2(0, y), new Vector2(width, y), color, 1f);
        }
    }

    private void DrawCachedLine(Renderer renderer, Vector2 start, Vector2 end, Color color, float thickness)
    {
        var direction = end - start;
        var length = direction.Length();
        if (length < 0.001f) return;

        var angle = MathF.Atan2(direction.Y, direction.X) * 180f / MathF.PI;

        _cachedLine.Size = new Vector2f(length, thickness);
        _cachedLine.Position = new Vector2f(start.X, start.Y);
        _cachedLine.FillColor = color;
        _cachedLine.Rotation = angle;
        _cachedLine.Origin = new Vector2f(0, thickness / 2);

        renderer.Window.Draw(_cachedLine);
    }

    private void DrawCachedCircle(Renderer renderer, Vector2 center, float radius, Color fillColor, Color outlineColor, float outlineThickness)
    {
        _cachedCircle.Radius = radius;
        _cachedCircle.Position = new Vector2f(center.X - radius, center.Y - radius);
        _cachedCircle.FillColor = fillColor;
        _cachedCircle.OutlineColor = outlineColor;
        _cachedCircle.OutlineThickness = outlineThickness;

        renderer.Window.Draw(_cachedCircle);
    }

    private void DrawCachedRectangle(Renderer renderer, Vector2 position, Vector2 size, Color fillColor, Color outlineColor, float outlineThickness)
    {
        _cachedRect.Size = new Vector2f(size.X, size.Y);
        _cachedRect.Position = new Vector2f(position.X, position.Y);
        _cachedRect.FillColor = fillColor;
        _cachedRect.OutlineColor = outlineColor;
        _cachedRect.OutlineThickness = outlineThickness;

        renderer.Window.Draw(_cachedRect);
    }

    private void DrawShapes(Renderer renderer)
    {
        renderer.Window.SetView(renderer.GameView);

        foreach (var shape in _shapes)
        {
            switch (shape.Type)
            {
                case ShapeType.Polygon:
                    if (shape.Points != null && shape.Points.Length >= 3)
                    {
                        DrawCachedPolygon(renderer, shape.Points, new Color(100, 200, 100, 50), new Color(100, 200, 100), 2f);
                    }
                    break;

                case ShapeType.Circle:
                    DrawCachedCircle(renderer, shape.Center, shape.Radius,
                        new Color(100, 150, 200, 50),
                        new Color(100, 150, 200), 2f);
                    break;

                case ShapeType.Rectangle:
                    DrawCachedRectangle(renderer, shape.Position,
                        new Vector2(shape.Width, shape.Height),
                        new Color(200, 150, 100, 50),
                        new Color(200, 150, 100), 2f);
                    break;
            }
        }
    }

    private void DrawCachedPolygon(Renderer renderer, Vector2[] points, Color fillColor, Color outlineColor, float outlineThickness)
    {
        if (points.Length < 3) return;

        // Draw filled polygon
        _cachedConvex.SetPointCount((uint)points.Length);
        for (int i = 0; i < points.Length; i++)
        {
            _cachedConvex.SetPoint((uint)i, new Vector2f(points[i].X, points[i].Y));
        }
        _cachedConvex.FillColor = fillColor;
        _cachedConvex.OutlineColor = outlineColor;
        _cachedConvex.OutlineThickness = outlineThickness;

        renderer.Window.Draw(_cachedConvex);
    }

    private void DrawCurrentDrawing(Renderer renderer)
    {
        renderer.Window.SetView(renderer.GameView);

        Vector2 snappedMouse = SnapToGrid(_currentMousePos);

        // Draw cursor snap indicator
        DrawCachedCircle(renderer, snappedMouse, 4, Color.Yellow, Color.White, 1f);

        switch (_currentMode)
        {
            case DrawMode.Polygon:
                DrawPolygonPreview(renderer, snappedMouse);
                break;

            case DrawMode.Circle:
                if (_drawStartPoint.HasValue && _isDrawing)
                {
                    DrawCirclePreview(renderer, _drawStartPoint.Value, snappedMouse);
                }
                break;

            case DrawMode.Rectangle:
                if (_drawStartPoint.HasValue && _isDrawing)
                {
                    DrawRectanglePreview(renderer, _drawStartPoint.Value, snappedMouse);
                }
                break;
        }
    }

    private void DrawPolygonPreview(Renderer renderer, Vector2 snappedMouse)
    {
        if (_currentPolygonPoints.Count == 0) return;

        // Draw existing points and lines
        for (int i = 0; i < _currentPolygonPoints.Count; i++)
        {
            // Draw point
            DrawCachedCircle(renderer, _currentPolygonPoints[i], 5,
                i == 0 ? Color.Green : Color.Cyan, Color.White, 1f);

            // Draw line to next point
            if (i < _currentPolygonPoints.Count - 1)
            {
                DrawCachedLine(renderer, _currentPolygonPoints[i], _currentPolygonPoints[i + 1],
                    new Color(100, 200, 100), 2f);
            }
        }

        // Draw line from last point to mouse
        DrawCachedLine(renderer, _currentPolygonPoints[^1], snappedMouse,
            new Color(100, 200, 100, 128), 2f);

        // If close to first point, highlight it
        if (_currentPolygonPoints.Count >= 3)
        {
            float distToFirst = Vector2.Distance(snappedMouse, _currentPolygonPoints[0]);
            if (distToFirst < GRID_SIZE)
            {
                DrawCachedCircle(renderer, _currentPolygonPoints[0], 8,
                    new Color(0, 255, 0, 100), Color.Green, 2f);
            }
        }
    }

    private void DrawCirclePreview(Renderer renderer, Vector2 start, Vector2 end)
    {
        float size = Math.Max(Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y));
        Vector2 center = start + new Vector2(size / 2, size / 2);
        float radius = size / 2;

        DrawCachedCircle(renderer, center, radius,
            new Color(100, 150, 200, 50),
            new Color(100, 150, 200, 200), 2f);
    }

    private void DrawRectanglePreview(Renderer renderer, Vector2 start, Vector2 end)
    {
        float minX = Math.Min(start.X, end.X);
        float minY = Math.Min(start.Y, end.Y);
        float width = Math.Abs(end.X - start.X);
        float height = Math.Abs(end.Y - start.Y);

        DrawCachedRectangle(renderer,
            new Vector2(minX, minY),
            new Vector2(width, height),
            new Color(200, 150, 100, 50),
            new Color(200, 150, 100, 200), 2f);
    }

    public void Shutdown()
    {
        _uiManager.Clear();
        _shapes.Clear();
        _constraints.Clear();
        _currentPolygonPoints.Clear();

        // Dispose cached SFML shapes
        _cachedLine.Dispose();
        _cachedCircle.Dispose();
        _cachedRect.Dispose();
        _cachedConvex.Dispose();
        _cachedToolbarBg.Dispose();

        // Unpause physics for other games
        _engine.PhysicsSystem.IsPaused = false;
    }
}
