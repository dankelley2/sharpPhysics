#nullable enable
using System;
using System.Numerics;
using physics.Engine.Helpers;
using SFML.Graphics;
using SFML.System;
using SFML.Window;
using SharpPhysics.Engine.Core;

namespace SharpPhysics.Rendering;

/// <summary>
/// Manages SFML rendering with separate views for background, game world, and UI layers.
/// </summary>
public class Renderer : IDisposable
{
    private readonly PhysicsSystem _physicsSystem;
    private readonly Font _defaultFont;
    private readonly Text _reusableText;
    private readonly VertexArray _lineRenderer = new(PrimitiveType.Lines, 2);
    private readonly CircleShape _circleShapeRenderer = new();
    private readonly RectangleShape _rectangleShapeRenderer = new();
    private readonly Color _constraintAColor = new(255, 0, 0, 100);
    private readonly Color _constraintBColor = new(0, 255, 255, 100);
    private bool _disposed;

    public RenderWindow Window { get; }
    public View GameView { get; }
    public View UiView { get; }
    public View BackgroundView { get; }
    public Font DefaultFont => _defaultFont;

    public Renderer(uint windowWidth, uint windowHeight, string windowTitle, PhysicsSystem physicsSystem)
    {
        var settings = new ContextSettings { AntialiasingLevel = 8 };
        Window = new RenderWindow(new VideoMode(windowWidth, windowHeight), windowTitle, Styles.Close, settings);
        Window.Closed += (s, e) => Window.Close();
        Window.SetFramerateLimit(144);

        GameView = new View(new FloatRect(0, 0, windowWidth, windowHeight));
        UiView = new View(new FloatRect(0, 0, windowWidth, windowHeight));
        BackgroundView = new View(new FloatRect(0, 0, windowWidth, windowHeight));

        _physicsSystem = physicsSystem;
        _defaultFont = new Font("Resources/good_timing_bd.otf");
        _reusableText = new Text("", _defaultFont, 24);
    }

    #region Frame Lifecycle

    public void BeginFrame(Color backColor)
    {
        Window.SetView(BackgroundView);
        Window.Clear(backColor);
    }

    /// <summary>
    /// Renders physics objects and constraints. Call between game background and foreground rendering.
    /// </summary>
    public void RenderPhysicsObjects()
    {
        Window.SetView(GameView);

        foreach (var obj in _physicsSystem.ListStaticObjects)
        {
            var sfmlShader = obj.Shader;
            if (sfmlShader != null)
            {
                sfmlShader.PreDraw(obj, Window);
                sfmlShader.Draw(obj, Window);
                sfmlShader.PostDraw(obj, Window);
            }
        }

        foreach (var obj in _physicsSystem.Constraints)
        {
            var a = obj.A.Center + PhysMath.RotateVector(obj.AnchorA, obj.A.Angle);
            var b = obj.B.Center + PhysMath.RotateVector(obj.AnchorB, obj.B.Angle);
            DrawLine(obj.A.Center, a, _constraintAColor);
            DrawLine(obj.B.Center, b, _constraintBColor);
        }
    }

    public void Display()
    {
        Window.Display();
    }

    #endregion

    #region View Pan/Zoom

    public void PanView(Vector2 delta)
    {
        GameView.Center += new Vector2f(delta.X, delta.Y);
    }

    public void PanViewByScreenDelta(Vector2 previousScreenPos, Vector2 currentScreenPos)
    {
        var prevWorld = Window.MapPixelToCoords(
            new Vector2i((int)previousScreenPos.X, (int)previousScreenPos.Y), GameView);
        var currWorld = Window.MapPixelToCoords(
            new Vector2i((int)currentScreenPos.X, (int)currentScreenPos.Y), GameView);

        var delta = new Vector2(prevWorld.X - currWorld.X, prevWorld.Y - currWorld.Y);
        PanView(delta);
    }

    /// <summary>
    /// Zooms the game view. Positive delta zooms in, negative zooms out.
    /// </summary>
    /// <param name="focusScreenPos">Screen position to keep stationary during zoom (e.g., mouse position).</param>
    public void ZoomView(float zoomDelta, Vector2? focusScreenPos = null)
    {
        float zoomFactor = 1f - (zoomDelta * 0.1f);
        zoomFactor = Math.Clamp(zoomFactor, 0.5f, 2f);

        if (focusScreenPos.HasValue)
        {
            var worldPosBefore = Window.MapPixelToCoords(
                new Vector2i((int)focusScreenPos.Value.X, (int)focusScreenPos.Value.Y), GameView);

            GameView.Zoom(zoomFactor);

            var worldPosAfter = Window.MapPixelToCoords(
                new Vector2i((int)focusScreenPos.Value.X, (int)focusScreenPos.Value.Y), GameView);

            var offset = new Vector2f(
                worldPosBefore.X - worldPosAfter.X,
                worldPosBefore.Y - worldPosAfter.Y);
            GameView.Center += offset;
        }
        else
        {
            GameView.Zoom(zoomFactor);
        }
    }

    public void ResetView(uint windowWidth, uint windowHeight)
    {
        GameView.Reset(new FloatRect(0, 0, windowWidth, windowHeight));
    }

    #endregion

    #region Drawing Primitives

    public void DrawText(string text, float x, float y, uint size = 24, Color? color = null)
    {
        Window.SetView(UiView);
        _reusableText.DisplayedString = text;
        _reusableText.CharacterSize = size;
        _reusableText.Position = new Vector2f(x, y);
        _reusableText.FillColor = color ?? Color.White;
        Window.Draw(_reusableText);
    }

    public void DrawLine(Vector2 start, Vector2 end, Color color)
    {
        Window.SetView(GameView);
        _lineRenderer[0] = new Vertex(new Vector2f(start.X, start.Y), color);
        _lineRenderer[1] = new Vertex(new Vector2f(end.X, end.Y), color);
        Window.Draw(_lineRenderer);
    }

    public void DrawCircle(Vector2 center, float radius, Color fillColor, Color? outlineColor = null, float outlineThickness = 2f)
    {
        Window.SetView(GameView);
        _circleShapeRenderer.Radius = radius;
        _circleShapeRenderer.Position = new Vector2f(center.X - radius, center.Y - radius);
        _circleShapeRenderer.FillColor = fillColor;
        _circleShapeRenderer.OutlineColor = outlineColor ?? Color.White;
        _circleShapeRenderer.OutlineThickness = outlineThickness;
        Window.Draw(_circleShapeRenderer);
    }

    public void DrawRectangle(Vector2 position, Vector2 size, Color fillColor, Color? outlineColor = null, float outlineThickness = 0f)
    {
        Window.SetView(GameView);
        _rectangleShapeRenderer.Size = new Vector2f(size.X, size.Y);
        _rectangleShapeRenderer.Position = new Vector2f(position.X, position.Y);
        _rectangleShapeRenderer.FillColor = fillColor;
        _rectangleShapeRenderer.OutlineColor = outlineColor ?? Color.Transparent;
        _rectangleShapeRenderer.OutlineThickness = outlineThickness;
        Window.Draw(_rectangleShapeRenderer);
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            _reusableText.Dispose();
            _defaultFont.Dispose();
            _lineRenderer.Dispose();
            _circleShapeRenderer.Dispose();
            _rectangleShapeRenderer.Dispose();
            GameView.Dispose();
            UiView.Dispose();
            BackgroundView.Dispose();
            Window.Dispose();
        }

        _disposed = true;
    }

    #endregion
}
