#nullable enable
using System.Numerics;
using physics.Engine.Core;
using SFML.Graphics;
using SharpPhysics.Engine.Core;
using SharpPhysics.Rendering;
using SharpPhysics.Rendering.Shaders;
using SharpPhysics.Rendering.UI;

namespace SharpPhysics.Demo.DemoProps;

/// <summary>
/// Debug UI panel for the physics sandbox with controls for gravity, simulation speed, etc.
/// </summary>
public class SandboxDebugUI
{
    private readonly UiManager _uiManager = new();
    private readonly GameEngine _engine;
    private readonly PhysicsSystem _physics;
    private UiButton? _pauseButton;

    public bool IsVisible { get; set; } = true;

    public SandboxDebugUI(GameEngine engine)
    {
        _engine = engine;
        _physics = engine.PhysicsSystem;
        InitializeControls();
    }

    private void InitializeControls()
    {
        var font = _engine.Renderer.DefaultFont;

        // Contact normals toggle
        var normalsLabel = new UiTextLabel("Contact Normals", font) { Position = new Vector2(200, 30), CharacterSize = 14 };
        var normalsCheckbox = new UiCheckbox(new Vector2(350, 30), new Vector2(20, 20)) { IsChecked = SFMLPolyShader.DrawNormals };
        normalsCheckbox.OnClick += isChecked => SFMLPolyShader.DrawNormals = isChecked;
        _uiManager.Add(normalsLabel);
        _uiManager.Add(normalsCheckbox);

        // Pause button
        _pauseButton = new UiButton("Pause", font, new Vector2(450, 30), new Vector2(70, 20));
        _pauseButton.OnClick += _ =>
        {
            _physics.IsPaused = !_physics.IsPaused;
            _pauseButton.Text = _physics.IsPaused ? "Resume" : "Pause";
        };
        _uiManager.Add(_pauseButton);

        // Gravity X slider
        _uiManager.Add(new UiTextLabel("Gravity X", font) { Position = new Vector2(200, 60), CharacterSize = 14 });
        var gravityXSlider = new UiSlider(new Vector2(200, 80), new Vector2(150, 20), -20f, 20f, _physics.Gravity.X);
        gravityXSlider.OnValueChanged += v => _physics.Gravity = new Vector2(v, _physics.Gravity.Y);
        _uiManager.Add(gravityXSlider);

        // Gravity Y slider
        _uiManager.Add(new UiTextLabel("Gravity Y", font) { Position = new Vector2(400, 60), CharacterSize = 14 });
        var gravityYSlider = new UiSlider(new Vector2(400, 80), new Vector2(150, 20), -20f, 20f, _physics.Gravity.Y);
        gravityYSlider.OnValueChanged += v => _physics.Gravity = new Vector2(_physics.Gravity.X, v);
        _uiManager.Add(gravityYSlider);

        // Simulation speed slider
        _uiManager.Add(new UiTextLabel("Simulation Speed", font) { Position = new Vector2(200, 110), CharacterSize = 14 });
        var simSpeedSlider = new UiSlider(new Vector2(200, 130), new Vector2(150, 20), 0.1f, 2f, _physics.TimeScale);
        simSpeedSlider.OnValueChanged += v => _physics.TimeScale = v;
        _uiManager.Add(simSpeedSlider);
    }

    public bool HandleClick(Vector2 uiPosition) => IsVisible && _uiManager.HandleClick(uiPosition);

    public void HandleDrag(Vector2 uiPosition) => _uiManager.HandleDrag(uiPosition);

    public void StopDrag() => _uiManager.StopDrag();

    public bool IsDragging => _uiManager.DraggedElement != null;

    public void Render(Renderer renderer)
    {
        if (!IsVisible) return;

        renderer.Window.SetView(renderer.UiView);

        // Performance stats
        renderer.DrawText(
            $"ms physics time: {_engine.MsPhysicsTime}\n" +
            $"ms draw time: {_engine.MsDrawTime}\n" +
            $"frame rate: {1000 / Math.Max(_engine.MsFrameTime, 1)}\n" +
            $"num objects: {_physics.ListStaticObjects.Count}",
            40, 40, 12, Color.White);

        _uiManager.Draw(renderer.Window);
    }

    public void Clear() => _uiManager.Clear();
}
