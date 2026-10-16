#nullable enable
using System;
using System.Diagnostics;

namespace SharpPhysics.Engine.Core;

/// <summary>
/// Per-frame timing breakdown in milliseconds, with garbage collector activity during the frame.
/// </summary>
public readonly record struct FrameTimings(
    long FrameNumber,
    double TotalMs,
    double EventsMs,
    double UpdateMs,
    double PhysicsMs,
    double RenderMs,
    double DisplayMs,
    int PhysicsSteps,
    double GcPauseMs,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections)
{
    public string SlowestPhase
    {
        get
        {
            var (name, ms) = ("events", EventsMs);
            if (UpdateMs > ms) (name, ms) = ("update", UpdateMs);
            if (PhysicsMs > ms) (name, ms) = ("physics", PhysicsMs);
            if (RenderMs > ms) (name, ms) = ("render", RenderMs);
            if (DisplayMs > ms) (name, ms) = ("display", DisplayMs);
            if (GcPauseMs > ms) name = "gc";
            return name;
        }
    }

    public override string ToString() =>
        $"frame {FrameNumber}: {TotalMs:F1} ms [slowest: {SlowestPhase}] " +
        $"events {EventsMs:F1}, update {UpdateMs:F1}, physics {PhysicsMs:F1} ({PhysicsSteps} steps), " +
        $"render {RenderMs:F1}, display {DisplayMs:F1} | GC pause {GcPauseMs:F1} ms, " +
        $"collections g0/g1/g2 {Gen0Collections}/{Gen1Collections}/{Gen2Collections}";
}

/// <summary>
/// Times each phase of the game loop and flags frames that take much longer than usual.
/// Set <see cref="BreakOnStutter"/> to stop in the debugger when a spike happens.
/// </summary>
public class FrameProfiler
{
    private const double AverageSmoothing = 0.05;
    private const int WarmupFrames = 120;
    // Below this, spikes are within normal vsync/timer jitter.
    private const double MinRelativeStutterMs = 16.7;

    private long _frameStart;
    private long _sectionStart;
    private double _eventsMs, _updateMs, _physicsMs, _renderMs;
    private TimeSpan _gcPauseStart;
    private int _gen0Start, _gen1Start, _gen2Start;
    private long _frameNumber;
    private int _framesSinceReset;

    /// <summary>Frames longer than this are always reported.</summary>
    public double StutterThresholdMs { get; set; } = 50;

    /// <summary>Frames longer than this multiple of the running average are reported.</summary>
    public double StutterAverageMultiplier { get; set; } = 4;

    /// <summary>Calls <see cref="Debugger.Break"/> on a stutter when a debugger is attached.</summary>
    public bool BreakOnStutter { get; set; }

    public bool LogStutters { get; set; } = true;

    public FrameTimings LastFrame { get; private set; }
    public FrameTimings? LastStutter { get; private set; }
    public double AverageFrameMs { get; private set; }

    public event Action<FrameTimings>? StutterDetected;

    /// <summary>
    /// Restarts warm-up so scene loads don't count as stutters or skew the average.
    /// </summary>
    public void Reset()
    {
        _framesSinceReset = 0;
        AverageFrameMs = 0;
    }

    public void BeginFrame()
    {
        _frameStart = Stopwatch.GetTimestamp();
        _sectionStart = _frameStart;
        _gcPauseStart = GC.GetTotalPauseDuration();
        _gen0Start = GC.CollectionCount(0);
        _gen1Start = GC.CollectionCount(1);
        _gen2Start = GC.CollectionCount(2);
    }

    public void MarkEvents() => _eventsMs = Lap();
    public void MarkUpdate() => _updateMs = Lap();
    public void MarkPhysics() => _physicsMs = Lap();
    public void MarkRender() => _renderMs = Lap();

    public void EndFrame(int physicsSteps)
    {
        double displayMs = Lap();
        double totalMs = Stopwatch.GetElapsedTime(_frameStart).TotalMilliseconds;

        var timings = new FrameTimings(
            ++_frameNumber, totalMs, _eventsMs, _updateMs, _physicsMs, _renderMs, displayMs, physicsSteps,
            (GC.GetTotalPauseDuration() - _gcPauseStart).TotalMilliseconds,
            GC.CollectionCount(0) - _gen0Start,
            GC.CollectionCount(1) - _gen1Start,
            GC.CollectionCount(2) - _gen2Start);
        LastFrame = timings;

        _framesSinceReset++;
        if (_framesSinceReset > WarmupFrames && IsStutter(totalMs))
        {
            ReportStutter(timings);
            return;
        }

        AverageFrameMs = AverageFrameMs == 0 ? totalMs : AverageFrameMs + (totalMs - AverageFrameMs) * AverageSmoothing;
    }

    private bool IsStutter(double totalMs)
    {
        double relativeLimit = Math.Max(MinRelativeStutterMs, AverageFrameMs * StutterAverageMultiplier);
        return totalMs > Math.Min(StutterThresholdMs, relativeLimit);
    }

    private void ReportStutter(FrameTimings timings)
    {
        LastStutter = timings;
        if (LogStutters)
            Console.WriteLine($"[Stutter] {timings} (avg {AverageFrameMs:F1} ms)");

        StutterDetected?.Invoke(timings);

        if (BreakOnStutter && Debugger.IsAttached)
        {
            // Inspect 'timings' to see which phase spiked.
            Debugger.Break();
        }
    }

    private double Lap()
    {
        long now = Stopwatch.GetTimestamp();
        double ms = Stopwatch.GetElapsedTime(_sectionStart, now).TotalMilliseconds;
        _sectionStart = now;
        return ms;
    }
}
