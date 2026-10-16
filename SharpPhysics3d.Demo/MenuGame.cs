#nullable enable
using System.Numerics;
using SFML.Graphics;
using SFML.Window;
using SharpPhysics.Engine.Core;
using SharpPhysics.Engine.Input;
using SharpPhysics.Rendering;
using SharpPhysics.Rendering.UI;

namespace SharpPhysics3d.Demo;

public class MenuGame : IGame
{
    private GameEngine _engine = null!;
    private readonly UiManager _uiManager = new();
    private readonly List<UiMenuButton> _buttons = new();

    public void Initialize(GameEngine engine)
    {
        _engine = engine;
        engine.PhysicsSystem.Gravity = Vector3.Zero;

        var size = new Vector2(420, 70);
        float x = engine.WindowWidth / 2f - size.X / 2;
        AddButton("Pool Table", "Top-down scene: spheres,\ncushions and tumbling crates", new Vector2(x, 220), size,
            () => engine.SwitchGame(new PoolTableGame()));
        AddButton("Quit", "Close the demo", new Vector2(x, 310), size,
            () => engine.Renderer.Window.Close());
    }

    private void AddButton(string title, string description, Vector2 position, Vector2 size, Action onClick)
    {
        var button = new UiMenuButton(title, description, _engine.Renderer.DefaultFont, position, size);
        button.OnClick += _ => onClick();
        _buttons.Add(button);
        _uiManager.Add(button);
    }

    public void Update(float deltaTime, InputManager inputManager)
    {
        var mouse = inputManager.MouseScreenPosition;
        foreach (var button in _buttons)
            button.SetHovered(button.ContainsPoint(mouse));

        if (inputManager.IsMousePressed(Mouse.Button.Left))
            _uiManager.HandleClick(mouse);
    }

    public void Render(Renderer renderer)
    {
        renderer.DrawText("SharpPhysics 3D", _engine.WindowWidth / 2f - 160, 90, 48, Color.White);
        renderer.DrawText("Select a scene", _engine.WindowWidth / 2f - 70, 160, 20, new Color(180, 180, 200));

        renderer.Window.SetView(renderer.UiView);
        _uiManager.Draw(renderer.Window);
    }

    public void Shutdown()
    {
        _uiManager.Clear();
        _buttons.Clear();
    }
}
