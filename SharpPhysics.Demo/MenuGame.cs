#nullable enable
using System.Numerics;
using physics.Engine.Core;
using physics.Engine.Input;
using physics.Engine.Rendering;
using physics.Engine.Rendering.UI;
using SharpPhysics.Demo.Designer;
using SharpPhysics.Demo.Helpers;
using SFML.Graphics;
using SFML.Window;

namespace SharpPhysics.Demo;

public class MenuGame : IGame
{
    private GameEngine _engine = null!;
    private UiManager _uiManager = new();
    private AnimatedBackground _background = null!;

    private readonly List<UiMenuButton> _menuButtons = new();
    private float _animationTime;

    private static readonly Dictionary<string, Func<IGame>> GameFactories = new()
    {
        ["RainCatcher"] = () => new RainCatcherGame(),
        ["BubblePop"] = () => new BubblePopGame(),
        ["Platformer"] = () => new PlatformerGame(),
        ["Sandbox"] = () => new DemoGame(),
        ["PrefabDesigner"] = () => new PrefabDesignerGame(),
        ["Settings"] = () => new SettingsGame(),
    };

    public void Initialize(GameEngine engine)
    {
        _engine = engine;
        _engine.PhysicsSystem.Gravity = Vector2.Zero;
        _engine.PhysicsSystem.GravityScale = 0;

        _background = new AnimatedBackground(engine.WindowWidth, engine.WindowHeight);
        CreateMenuUI();

        Console.WriteLine("Menu loaded - click a button to start a game!");
    }

    private void CreateMenuUI()
    {
        var font = _engine.Renderer.DefaultFont;
        float centerX = _engine.WindowWidth / 2f;
        float startY = 145f;
        float buttonWidth = 450f;
        float buttonHeight = 60f;
        float spacing = 12f;

        var buttonConfigs = new (string Key, string Title, string Subtitle, Color Base, Color Hover, Color Border)[]
        {
            ("RainCatcher", "Rain Catcher", "Catch falling balls with your body!", new(60, 60, 90), new(80, 80, 130), new(100, 180, 255)),
            ("BubblePop", "Bubble Pop", "Pop floating bubbles with your hands!", new(60, 80, 70), new(80, 120, 100), new(100, 255, 180)),
            ("Platformer", "Platformer", "Action platformer - keyboard controls!", new(90, 60, 60), new(130, 80, 80), new(255, 180, 100)),
            ("Sandbox", "Physics Sandbox", "Experiment with physics simulation", new(80, 60, 70), new(120, 80, 100), new(255, 150, 180)),
            ("PrefabDesigner", "Prefab Designer", "Design physics object prefabs and save to JSON", new(70, 70, 80), new(100, 100, 120), new(180, 180, 220)),
            ("Settings", "Settings", "Configure camera, detection, and display options", new(50, 50, 60), new(70, 70, 90), new(150, 150, 180)),
        };

        for (int i = 0; i < buttonConfigs.Length; i++)
        {
            var cfg = buttonConfigs[i];
            var button = new UiMenuButton(
                cfg.Title, cfg.Subtitle, font,
                new Vector2(centerX - buttonWidth / 2, startY + (buttonHeight + spacing) * i),
                new Vector2(buttonWidth, buttonHeight),
                baseColor: cfg.Base, hoverColor: cfg.Hover, borderColor: cfg.Border);
            button.OnClick += _ => SwitchToGame(cfg.Key);
            _menuButtons.Add(button);
            _uiManager.Add(button);
        }
    }

    private void SwitchToGame(string gameKey)
    {
        if (GameFactories.TryGetValue(gameKey, out var factory))
        {
            Console.WriteLine($"Starting {gameKey}...");
            _engine.SwitchGame(factory());
        }
    }

    public void Update(float deltaTime, InputManager inputManager)
    {
        _animationTime += deltaTime;
        _background.Update(deltaTime);

        if (inputManager.IsMousePressed(Mouse.Button.Left))
            _uiManager.HandleClick(inputManager.MousePosition);

        foreach (var button in _menuButtons)
            button.SetHovered(button.ContainsPoint(inputManager.MousePosition));
    }

    public void RenderBackground(Renderer renderer)
    {
        _background.Draw(renderer.Window);
    }

    public void Render(Renderer renderer)
    {
        float titleY = 50 + MathF.Sin(_animationTime * 2f) * 5f;
        renderer.DrawText("SharpPhysics Demo Games", _engine.WindowWidth / 2f - 300, titleY, 42, Color.White);
        renderer.DrawText("Select a game to play", _engine.WindowWidth / 2f - 100, 100, 20, new Color(180, 180, 200));
        renderer.DrawText("Use your body to interact! • Body tracking powered by YOLO Pose",
            _engine.WindowWidth / 2f - 250, _engine.WindowHeight - 30, 14, new Color(120, 120, 140));

        renderer.Window.SetView(renderer.UiView);
        _uiManager.Draw(renderer.Window);
    }

    public void Shutdown()
    {
        _background.Dispose();
        _uiManager.Clear();
        _menuButtons.Clear();
    }
}
