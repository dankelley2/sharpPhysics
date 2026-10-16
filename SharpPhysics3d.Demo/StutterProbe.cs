using SharpPhysics.Engine.Core;
using SharpPhysics.Engine.Input;
using SharpPhysics.Rendering;

namespace SharpPhysics3d.Demo;

public class StutterProbe : IGame
{
    private readonly PoolTableGame _game = new();
    private GameEngine _engine = null!;
    private float _time;
    private readonly List<double> _frames = new();

    public void Initialize(GameEngine engine) { _engine = engine; _game.Initialize(engine); }

    public void Update(float deltaTime, InputManager input)
    {
        _time += deltaTime;
        if (_engine.Profiler.LastFrame.EventsMs > 5)
            Console.WriteLine($"[Probe] t={_time:F2}s events spike {_engine.Profiler.LastFrame.EventsMs:F1} ms");
        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        SFML.Window.Joystick.Update();
        double joystickMs = System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        if (joystickMs > 5)
            Console.WriteLine($"[Probe] t={_time:F2}s Joystick.Update {joystickMs:F1} ms");
        _engine.Renderer.Camera.Orbit(0.6f * deltaTime, 0.2f * deltaTime);
        _game.Update(deltaTime, input);
        if (_time > 2f) _frames.Add(_engine.Profiler.LastFrame.TotalMs);
        if (_time > 22f)
        {
            _frames.Sort();
            Console.WriteLine($"[Probe] frames={_frames.Count} median={_frames[_frames.Count / 2]:F2} p99={_frames[(int)(_frames.Count * 0.99)]:F2} max={_frames[^1]:F2} avg={_engine.Profiler.AverageFrameMs:F2}");
            _engine.Renderer.Window.Close();
        }
    }

    public void Render(Renderer renderer) => _game.Render(renderer);

    public void Shutdown() => _game.Shutdown();
}
