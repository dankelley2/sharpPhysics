#nullable enable
using System.Numerics;
using physics.Engine;
using physics.Engine.Core;
using physics.Engine.Input;
using physics.Engine.Rendering;
using physics.Engine.Rendering.UI;
using physics.Engine.Shaders;
using physics.Engine.Objects;
using SFML.Graphics;

namespace SharpPhysics.Demo;

/// <summary>
/// Main menu for selecting between demo games.
/// Features animated background and styled buttons.
/// </summary>
public class MenuGame : IGame
{
    private GameEngine _engine = null!;
    private PhysicsSystem _physics = null!;
    private UiManager _uiManager = new();
    private Random _random = new();

    // Menu buttons
    private readonly List<UiMenuButton> _menuButtons = new();
    private float _animationTime;

    // Background particle system
    private readonly List<MenuParticle> _particles = new();
    private const int PARTICLE_COUNT = 50;

    // Game factories for switching
    private readonly Dictionary<string, Func<IGame>> _gameFactories = new()
    {
        ["RainCatcher"] = () => new RainCatcherGame(),
        ["BubblePop"] = () => new BubblePopGame(),
        ["Platformer"] = () => new PlatformerGame(),
        ["Sandbox"] = () => new DemoGame(),
        ["Settings"] = () => new SettingsGame(),
    };

    public void Initialize(GameEngine engine)
    {
        _engine = engine;
        _physics = engine.PhysicsSystem;

        // Hide debug UI in menu
        _engine.Renderer.ShowDebugUI = false;

        // Disable gravity for menu background
        _physics.Gravity = new Vector2(0, 0);
        _physics.GravityScale = 0;

        CreateMenuUI();
        CreateBackgroundParticles();

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

        // Rain Catcher button
        var rainButton = new UiMenuButton(
            "Rain Catcher",
            "Catch falling balls with your body!",
            font,
            new Vector2(centerX - buttonWidth / 2, startY),
            new Vector2(buttonWidth, buttonHeight),
            baseColor: new Color(60, 60, 90),
            hoverColor: new Color(80, 80, 130),
            borderColor: new Color(100, 180, 255)
        );
        rainButton.OnClick += _ => SwitchToGame("RainCatcher");
        _menuButtons.Add(rainButton);
        _uiManager.Add(rainButton);

        // Bubble Pop button
        var bubbleButton = new UiMenuButton(
            "Bubble Pop",
            "Pop floating bubbles with your hands!",
            font,
            new Vector2(centerX - buttonWidth / 2, startY + buttonHeight + spacing),
            new Vector2(buttonWidth, buttonHeight),
            baseColor: new Color(60, 80, 70),
            hoverColor: new Color(80, 120, 100),
            borderColor: new Color(100, 255, 180)
        );
        bubbleButton.OnClick += _ => SwitchToGame("BubblePop");
        _menuButtons.Add(bubbleButton);
        _uiManager.Add(bubbleButton);

        // Platformer button
        var platformerButton = new UiMenuButton(
            "Platformer",
            "Action platformer - keyboard controls!",
            font,
            new Vector2(centerX - buttonWidth / 2, startY + (buttonHeight + spacing) * 2),
            new Vector2(buttonWidth, buttonHeight),
            baseColor: new Color(90, 60, 60),
            hoverColor: new Color(130, 80, 80),
            borderColor: new Color(255, 180, 100)
        );
        platformerButton.OnClick += _ => SwitchToGame("Platformer");
        _menuButtons.Add(platformerButton);
        _uiManager.Add(platformerButton);

        // Physics Sandbox button
        var sandboxButton = new UiMenuButton(
            "Physics Sandbox",
            "Experiment with physics simulation",
            font,
            new Vector2(centerX - buttonWidth / 2, startY + (buttonHeight + spacing) * 3),
            new Vector2(buttonWidth, buttonHeight),
            baseColor: new Color(80, 60, 70),
            hoverColor: new Color(120, 80, 100),
            borderColor: new Color(255, 150, 180)
        );
        sandboxButton.OnClick += _ => SwitchToGame("Sandbox");
        _menuButtons.Add(sandboxButton);
        _uiManager.Add(sandboxButton);

        // Settings button
        var settingsButton = new UiMenuButton(
            "Settings",
            "Configure camera, detection, and display options",
            font,
            new Vector2(centerX - buttonWidth / 2, startY + (buttonHeight + spacing) * 4),
            new Vector2(buttonWidth, buttonHeight),
            baseColor: new Color(50, 50, 60),
            hoverColor: new Color(70, 70, 90),
            borderColor: new Color(150, 150, 180)
        );
        settingsButton.OnClick += _ => SwitchToGame("Settings");
        _menuButtons.Add(settingsButton);
        _uiManager.Add(settingsButton);

        // Hint at bottom
        var exitHint = new UiTextLabel("Press ESC to return to menu from any game", font)
        {
            Position = new Vector2(centerX - 200, _engine.WindowHeight - 50),
            CharacterSize = 14
        };
    }

    private void CreateBackgroundParticles()
    {
        for (int i = 0; i < PARTICLE_COUNT; i++)
        {
            _particles.Add(CreateParticle());
        }
    }

    private MenuParticle CreateParticle()
    {
        return new MenuParticle
        {
            X = _random.NextFloat() * _engine.WindowWidth,
            Y = _random.NextFloat() * _engine.WindowHeight,
            VelX = (_random.NextFloat() - 0.5f) * 30f,
            VelY = (_random.NextFloat() - 0.5f) * 30f,
            Size = _random.NextFloat() * 20f + 5f,
            Alpha = _random.NextFloat() * 0.3f + 0.1f,
            Hue = _random.NextFloat()
        };
    }

    private void SwitchToGame(string gameKey)
    {
        if (_gameFactories.TryGetValue(gameKey, out var factory))
        {
            Console.WriteLine($"Starting {gameKey}...");
            _engine.SwitchGame(factory());
        }
    }

    public void Update(float deltaTime, KeyState keyState)
    {
        _animationTime += deltaTime;

        // Update particle animation
        UpdateParticles(deltaTime);

        // Handle UI clicks
        if (keyState.LeftMousePressed)
        {
            _uiManager.HandleClick(keyState.MousePosition);
        }

        // Check for button hover
        foreach (var button in _menuButtons)
        {
            button.SetHovered(button.ContainsPoint(keyState.MousePosition));
        }
    }

    private void UpdateParticles(float deltaTime)
    {
        foreach (var p in _particles)
        {
            p.X += p.VelX * deltaTime;
            p.Y += p.VelY * deltaTime;

            // Wrap around screen
            if (p.X < -50) p.X = _engine.WindowWidth + 50;
            if (p.X > _engine.WindowWidth + 50) p.X = -50;
            if (p.Y < -50) p.Y = _engine.WindowHeight + 50;
            if (p.Y > _engine.WindowHeight + 50) p.Y = -50;

            // Slowly change hue
            p.Hue = (p.Hue + deltaTime * 0.05f) % 1f;
        }
    }

    public void Render(Renderer renderer)
    {
        // Draw animated title
        float titleY = 50 + MathF.Sin(_animationTime * 2f) * 5f;
        renderer.DrawText("SharpPhysics Demo Games", 
            _engine.WindowWidth / 2f - 300, titleY, 42, Color.White);

        // Draw subtitle
        renderer.DrawText("Select a game to play", 
            _engine.WindowWidth / 2f - 100, 100, 20, new Color(180, 180, 200));

        // Draw animated background particles
        DrawParticles(renderer);

            // Draw version/info at bottom
            renderer.DrawText("Use your body to interact! • Body tracking powered by YOLO Pose", 
                _engine.WindowWidth / 2f - 250, _engine.WindowHeight - 30, 14, new Color(120, 120, 140));

            // Draw UI elements (menu buttons)
            renderer.Window.SetView(renderer.UiView);
            _uiManager.Draw(renderer.Window);
        }

        private void DrawParticles(Renderer renderer)
    {
        foreach (var p in _particles)
        {
            // Convert hue to RGB (simple HSV to RGB)
            var color = HsvToColor(p.Hue, 0.6f, 0.8f, (byte)(p.Alpha * 255));

            var circle = new CircleShape(p.Size)
            {
                Position = new SFML.System.Vector2f(p.X - p.Size, p.Y - p.Size),
                FillColor = color
            };
            renderer.Window.Draw(circle);
        }
    }

    private Color HsvToColor(float h, float s, float v, byte alpha)
    {
        int hi = (int)(h * 6) % 6;
        float f = h * 6 - (int)(h * 6);
        float p = v * (1 - s);
        float q = v * (1 - f * s);
        float t = v * (1 - (1 - f) * s);

        float r, g, b;
        switch (hi)
        {
            case 0: r = v; g = t; b = p; break;
            case 1: r = q; g = v; b = p; break;
            case 2: r = p; g = v; b = t; break;
            case 3: r = p; g = q; b = v; break;
            case 4: r = t; g = p; b = v; break;
            default: r = v; g = p; b = q; break;
        }

        return new Color((byte)(r * 255), (byte)(g * 255), (byte)(b * 255), alpha);
    }

    public void Shutdown()
    {
        _uiManager.Clear();
        _particles.Clear();
        _menuButtons.Clear();
    }

    private class MenuParticle
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float VelX { get; set; }
        public float VelY { get; set; }
        public float Size { get; set; }
        public float Alpha { get; set; }
        public float Hue { get; set; }
    }
}
