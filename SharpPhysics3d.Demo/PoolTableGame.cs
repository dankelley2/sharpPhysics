#nullable enable
using System.Numerics;
using SFML.Graphics;
using SFML.Window;
using SharpPhysics.Engine.Core;
using SharpPhysics.Engine.Input;
using SharpPhysics.Engine.Objects;
using SharpPhysics.Engine.Shapes;
using SharpPhysics.Rendering;
using SharpPhysics.Rendering.Shaders;
using SharpPhysics.Rendering.UI;

namespace SharpPhysics3d.Demo;

/// <summary>
/// Pool table on the XY plane with gravity along +Z (away from the default camera).
/// Arrow keys orbit the camera around the table to inspect the 3D simulation.
/// </summary>
public class PoolTableGame : IGame
{
    private const float BallRadius = 12f;
    private const float TableWidth = 900f;
    private const float TableHeight = 450f;
    private const float CushionThickness = 22f;
    private const float RailThickness = 40f;
    private const float PocketRadius = 22f;
    private const float RollingResistance = 0.5f;
    private const float MaxShotSpeed = 2500f;
    private const float FallenDepth = 400f;
    private const float CameraOrbitSpeed = 1.2f;

    private static readonly Color FeltColor = new(30, 140, 60);
    private static readonly Color CushionColor = new(70, 165, 50);
    private static readonly Color RailColor = new(160, 90, 45);
    private static readonly Color CrateColor = new(200, 150, 80);
    private static readonly Color AimColor = new(255, 255, 255, 160);
    private static readonly Color PullColor = new(160, 160, 160);

    // Rack order; index 4 is the center of the third row, where the 8-ball goes.
    private static readonly Color[] RackColors =
    {
        new(250, 200, 30), new(30, 70, 200), new(210, 40, 40), new(110, 40, 160), new(20, 20, 20),
        new(240, 120, 20), new(20, 130, 60), new(130, 30, 40), new(255, 225, 110), new(110, 140, 230),
        new(240, 110, 110), new(170, 120, 210), new(250, 170, 100), new(90, 190, 120), new(190, 90, 100),
    };

    private GameEngine _engine = null!;
    private Camera3D _camera = null!;
    private readonly UiManager _uiManager = new();
    private UiButton _pauseButton = null!;
    private UiSlider _powerSlider = null!;

    private readonly List<PhysicsObject> _balls = new();
    private readonly List<PhysicsObject> _crates = new();
    private readonly Vector2[] _pockets = new Vector2[6];
    private readonly Random _random = new();
    private PhysicsObject? _cueBall;
    private Vector2 _tableCenter;
    private bool _aiming;
    private bool _uiCaptured;
    private int _potted;

    private float Left => _tableCenter.X - TableWidth / 2;
    private float Right => _tableCenter.X + TableWidth / 2;
    private float Top => _tableCenter.Y - TableHeight / 2;
    private float Bottom => _tableCenter.Y + TableHeight / 2;
    private Vector2 HeadSpot => new(Left + TableWidth / 4, _tableCenter.Y);
    private Vector2 FootSpot => new(Right - TableWidth / 4, _tableCenter.Y);
    private Vector2 OuterSize => new(TableWidth + 2 * (CushionThickness + RailThickness), TableHeight + 2 * (CushionThickness + RailThickness));

    public void Initialize(GameEngine engine)
    {
        _engine = engine;
        _tableCenter = new Vector2(engine.WindowWidth / 2f, engine.WindowHeight / 2f + 40);

        engine.PhysicsSystem.Gravity = new Vector3(0, 0, 9.8f);
        engine.PhysicsSystem.SpatialHashCellSize = 64f;

        _camera = engine.Renderer.Camera;
        ResetCamera();

        BuildTable();
        RackBalls();
        CreateUi();
    }

    #region Scene Setup

    private void ResetCamera()
    {
        _camera.Reset(new Vector3(_tableCenter, 0f));
        // Orbit on a sphere roughly the size of the table.
        _camera.Distance = OuterSize.X;
    }

    private void BuildTable()
    {
        _pockets[0] = new Vector2(Left - 4, Top - 4);
        _pockets[1] = new Vector2(_tableCenter.X, Top - 6);
        _pockets[2] = new Vector2(Right + 4, Top - 4);
        _pockets[3] = new Vector2(Left - 4, Bottom + 4);
        _pockets[4] = new Vector2(_tableCenter.X, Bottom + 6);
        _pockets[5] = new Vector2(Right + 4, Bottom + 4);

        // Felt bed: top surface at z = 0, extends under the cushions and rails.
        var tableShader = new PoolTableShader(FeltColor, _pockets, PocketRadius,
            new Vector2(HeadSpot.X, Top), new Vector2(HeadSpot.X, Bottom));
        AddStaticBox(new Vector3(OuterSize, 40f), new Vector3(_tableCenter, 20f), 0f, 0.2f, tableShader);

        float height = BallRadius * 2.5f;
        float z = -height / 2;
        float cornerGap = PocketRadius * 1.5f;
        float sideGap = PocketRadius;
        float halfRailLength = TableWidth / 2 - cornerGap - sideGap;
        float endRailLength = TableHeight - 2 * cornerGap;
        var cushionShader = new SolidShader(CushionColor);
        var railShader = new SolidShader(RailColor);

        foreach (float y in new[] { Top - CushionThickness / 2, Bottom + CushionThickness / 2 })
        {
            AddStaticBox(new Vector3(halfRailLength, CushionThickness, height),
                new Vector3(Left + cornerGap + halfRailLength / 2, y, z), 0.75f, 0.1f, cushionShader);
            AddStaticBox(new Vector3(halfRailLength, CushionThickness, height),
                new Vector3(Right - cornerGap - halfRailLength / 2, y, z), 0.75f, 0.1f, cushionShader);
        }

        foreach (float x in new[] { Left - CushionThickness / 2, Right + CushionThickness / 2 })
        {
            AddStaticBox(new Vector3(CushionThickness, endRailLength, height),
                new Vector3(x, _tableCenter.Y, z), 0.75f, 0.1f, cushionShader);
        }

        // Outer rails stop balls that miss a pocket from leaving the table.
        float railOffsetX = TableWidth / 2 + CushionThickness + RailThickness / 2;
        float railOffsetY = TableHeight / 2 + CushionThickness + RailThickness / 2;
        foreach (float sign in new[] { -1f, 1f })
        {
            AddStaticBox(new Vector3(OuterSize.X, RailThickness, height),
                new Vector3(_tableCenter.X, _tableCenter.Y + sign * railOffsetY, z), 0.5f, 0.1f, railShader);
            AddStaticBox(new Vector3(RailThickness, OuterSize.Y, height),
                new Vector3(_tableCenter.X + sign * railOffsetX, _tableCenter.Y, z), 0.5f, 0.1f, railShader);
        }
    }

    private void AddStaticBox(Vector3 size, Vector3 center, float restitution, float friction, ObjectShader shader)
    {
        var box = new PhysicsObject(new BoxPhysShape(size.X, size.Y, size.Z), center, restitution, locked: true, shader)
        {
            Friction = friction
        };
        _engine.PhysicsSystem.ListStaticObjects.Add(box);
    }

    private void RackBalls()
    {
        _cueBall = SpawnBall(HeadSpot, Color.White);

        float dx = BallRadius * 2f * 0.866f + 0.5f;
        float dy = BallRadius * 2f + 0.5f;
        int index = 0;
        for (int row = 0; row < 5; row++)
        {
            for (int i = 0; i <= row; i++)
            {
                SpawnBall(new Vector2(FootSpot.X + row * dx, FootSpot.Y + (i - row / 2f) * dy), RackColors[index++]);
            }
        }
    }

    private PhysicsObject SpawnBall(Vector2 position, Color color)
    {
        var ball = new PhysicsObject(new SpherePhysShape(BallRadius), new Vector3(position, -BallRadius), 0.92f,
            locked: false, new SolidShader(color, specular: 0.6f), canRotate: true)
        {
            Friction = 0.2f
        };
        _balls.Add(ball);
        _engine.PhysicsSystem.ListStaticObjects.Add(ball);
        return ball;
    }

    private void DropCrate()
    {
        var position = new Vector3(
            _tableCenter.X + (_random.NextSingle() - 0.5f) * TableWidth * 0.8f,
            _tableCenter.Y + (_random.NextSingle() - 0.5f) * TableHeight * 0.8f,
            -200f);

        var crate = new PhysicsObject(new BoxPhysShape(40, 40, 40), position, 0.2f, locked: false, new SolidShader(CrateColor), canRotate: true)
        {
            Orientation = Quaternion.CreateFromYawPitchRoll(
                _random.NextSingle() * MathF.PI, _random.NextSingle() * MathF.PI, _random.NextSingle() * MathF.PI),
            Friction = 0.4f
        };
        _crates.Add(crate);
        _engine.PhysicsSystem.ListStaticObjects.Add(crate);
    }

    private void Reset()
    {
        foreach (var obj in _balls.Concat(_crates))
            _engine.PhysicsSystem.DestroyObject(obj);

        _balls.Clear();
        _crates.Clear();
        _cueBall = null;
        _aiming = false;
        _potted = 0;
        _engine.PhysicsSystem.ProcessRemovalQueue();
        RackBalls();
    }

    private void CreateUi()
    {
        AddButton("Menu", 10, 90, () => _engine.SwitchGame(new MenuGame()));
        AddButton("Reset", 110, 90, Reset);
        _pauseButton = AddButton("Pause", 210, 90, TogglePause);
        AddButton("Drop Crate", 310, 120, DropCrate);

        _powerSlider = new UiSlider(new Vector2(460, 24), new Vector2(180, 14), 0.5f, 4f, 1.5f);
        _uiManager.Add(_powerSlider);

        var slowMotion = new UiCheckbox(new Vector2(670, 20), new Vector2(22, 22));
        slowMotion.OnClick += on => _engine.PhysicsSystem.TimeScale = on ? 0.25f : 1f;
        _uiManager.Add(slowMotion);
    }

    private UiButton AddButton(string text, float x, float width, Action onClick)
    {
        var button = new UiButton(text, _engine.Renderer.DefaultFont, new Vector2(x, 14), new Vector2(width, 36));
        button.OnClick += _ => onClick();
        _uiManager.Add(button);
        return button;
    }

    private void TogglePause()
    {
        _engine.PhysicsSystem.IsPaused = !_engine.PhysicsSystem.IsPaused;
        _pauseButton.Text = _engine.PhysicsSystem.IsPaused ? "Resume" : "Pause";
    }

    #endregion

    #region Update

    public void Update(float deltaTime, InputManager input)
    {
        HandleKeyboard(input);
        HandleCamera(deltaTime, input);
        HandleMouse(input);

        if (!_engine.PhysicsSystem.IsPaused)
            ApplyRollingResistance(deltaTime * _engine.PhysicsSystem.TimeScale);

        RemovePocketedAndFallen();
    }

    private void HandleKeyboard(InputManager input)
    {
        if (input.IsKeyPressed(Keyboard.Key.Space)) TogglePause();
        if (input.IsKeyPressed(Keyboard.Key.R)) Reset();
        if (input.IsKeyPressed(Keyboard.Key.C)) DropCrate();
        if (input.IsKeyPressed(Keyboard.Key.Home)) ResetCamera();
        if (input.IsKeyPressed(Keyboard.Key.Escape)) _engine.SwitchGame(new MenuGame());
    }

    private void HandleCamera(float deltaTime, InputManager input)
    {
        float step = CameraOrbitSpeed * deltaTime;
        float yaw = 0f, pitch = 0f;
        if (input.IsKeyHeld(Keyboard.Key.Left)) yaw += step;
        if (input.IsKeyHeld(Keyboard.Key.Right)) yaw -= step;
        if (input.IsKeyHeld(Keyboard.Key.Down)) pitch += step;
        if (input.IsKeyHeld(Keyboard.Key.Up)) pitch -= step;

        if (yaw != 0f || pitch != 0f)
            _camera.Orbit(yaw, pitch);
    }

    private void HandleMouse(InputManager input)
    {
        Vector2 screen = input.MouseScreenPosition;
        bool held = input.IsMouseHeld(Mouse.Button.Left);

        if (input.IsMousePressed(Mouse.Button.Left))
        {
            if (_uiManager.HandleClick(screen))
                _uiCaptured = true;
            else if (_cueBall != null && IsOverCueBall(input.MousePosition))
                _aiming = true;
        }

        if (held && _uiCaptured)
            _uiManager.HandleDrag(screen);

        if (held)
            return;

        if (_aiming && TryGetAimPoint(input.MousePosition, out var aimPoint))
            Shoot(aimPoint);

        _aiming = false;
        if (_uiCaptured)
        {
            _uiManager.StopDrag();
            _uiCaptured = false;
        }
    }

    private bool IsOverCueBall(Vector2 mouse)
    {
        var center = _cueBall!.Center;
        float projectedRadius = BallRadius * _camera.GetScale(center);
        return Vector2.Distance(_camera.Project(center), mouse) <= projectedRadius * 2.5f;
    }

    /// <summary>
    /// Casts the mouse ray onto the horizontal plane through the cue ball's center.
    /// </summary>
    private bool TryGetAimPoint(Vector2 mouse, out Vector3 aimPoint)
    {
        aimPoint = default;
        if (_cueBall == null)
            return false;

        var (origin, direction) = _camera.GetRay(mouse);
        if (MathF.Abs(direction.Z) < 1e-4f)
            return false;

        float t = (_cueBall.Center.Z - origin.Z) / direction.Z;
        if (t <= 0f)
            return false;

        aimPoint = origin + direction * t;
        return true;
    }

    private void Shoot(Vector3 aimPoint)
    {
        Vector3 pull = _cueBall!.Center - aimPoint;
        Vector2 velocity = new Vector2(pull.X, pull.Y) * _powerSlider.Value * 6f;
        if (velocity.Length() > MaxShotSpeed)
            velocity = Vector2.Normalize(velocity) * MaxShotSpeed;

        _cueBall.Velocity = new Vector3(velocity, 0f);
    }

    private void ApplyRollingResistance(float dt)
    {
        float k = MathF.Max(0f, 1f - RollingResistance * dt);
        foreach (var ball in _balls)
        {
            var v = ball.Velocity;
            ball.Velocity = new Vector3(v.X * k, v.Y * k, v.Z);
            ball.AngularVelocity *= k;
        }
    }

    private void RemovePocketedAndFallen()
    {
        for (int i = _balls.Count - 1; i >= 0; i--)
        {
            var ball = _balls[i];
            if (!IsInPocket(new Vector2(ball.Center.X, ball.Center.Y)) && ball.Center.Z < FallenDepth)
                continue;

            _engine.PhysicsSystem.DestroyObject(ball);
            _balls.RemoveAt(i);

            if (ball == _cueBall)
            {
                _cueBall = null;
                _aiming = false;
            }
            else
            {
                _potted++;
            }
        }

        _cueBall ??= SpawnBall(HeadSpot, Color.White);

        for (int i = _crates.Count - 1; i >= 0; i--)
        {
            if (_crates[i].Center.Z < FallenDepth)
                continue;

            _engine.PhysicsSystem.DestroyObject(_crates[i]);
            _crates.RemoveAt(i);
        }
    }

    private bool IsInPocket(Vector2 position)
    {
        foreach (var pocket in _pockets)
        {
            if (Vector2.DistanceSquared(position, pocket) <= PocketRadius * PocketRadius)
                return true;
        }
        return false;
    }

    #endregion

    #region Rendering

    public void Render(Renderer renderer)
    {
        if (_aiming && _cueBall != null && TryGetAimPoint(_engine.InputManager.MousePosition, out var aimPoint))
        {
            var cue = _cueBall.Center;
            renderer.DrawLine(cue, aimPoint, PullColor);
            renderer.DrawLine(cue, cue + (cue - aimPoint) * 2f, AimColor);
        }

        int objectBalls = _balls.Count - (_cueBall != null ? 1 : 0);
        renderer.DrawText($"Shot power {_powerSlider.Value:F1}x", 460, 42, 14);
        renderer.DrawText("Slow motion", 700, 20, 16);
        renderer.DrawText($"Balls: {objectBalls}  Potted: {_potted}  Crates: {_crates.Count}  Physics: {_engine.MsPhysicsTime} ms",
            830, 20, 16);
        renderer.DrawText("Drag back from the cue ball to shoot  |  Arrows: orbit camera  |  Home: top view  |  Space: pause  |  R: reset  |  C: crate  |  Esc: menu",
            10, _engine.WindowHeight - 26, 14, new Color(180, 180, 200));

        renderer.Window.SetView(renderer.UiView);
        _uiManager.Draw(renderer.Window);
    }

    public void Shutdown()
    {
        _uiManager.Clear();
    }

    #endregion
}
