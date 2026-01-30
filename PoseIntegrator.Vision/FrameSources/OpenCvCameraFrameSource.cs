using OpenCvSharp;
using PoseIntegrator.Vision.Abstractions;

namespace PoseIntegrator.Vision.FrameSources;

public sealed class OpenCvCameraFrameSource : IFrameSource
{
    private readonly VideoCapture _cap;

    public OpenCvCameraFrameSource(int cameraIndex = 0, int? width = null, int? height = null, int? fps = null)
    {
        // if platform is macOS or Linux, use V4L2 or AVFoundation
        var api = Environment.OSVersion.Platform switch
        {
            PlatformID.MacOSX => VideoCaptureAPIs.AVFOUNDATION,
            PlatformID.Unix   => VideoCaptureAPIs.AVFOUNDATION,
            _                 => VideoCaptureAPIs.DSHOW,
        };
        _cap = new VideoCapture(cameraIndex, api);
        if (!_cap.IsOpened())
            throw new InvalidOperationException($"Could not open camera index {cameraIndex}.");

        if (width is not null)  _cap.Set(VideoCaptureProperties.FrameWidth,  width.Value);
        if (height is not null) _cap.Set(VideoCaptureProperties.FrameHeight, height.Value);
        if (fps is not null)    _cap.Set(VideoCaptureProperties.Fps,         fps.Value);

        // After setting, check actual values:
        var actualWidth = _cap.Get(VideoCaptureProperties.FrameWidth);
        var actualHeight = _cap.Get(VideoCaptureProperties.FrameHeight);
        Console.WriteLine($"Requested: {width}x{height}, Actual: {actualWidth}x{actualHeight}");
    }

    public bool TryGetFrame(out Mat bgr, out long timestampMs)
    {
        bgr = new Mat();
        timestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (!_cap.Read(bgr) || bgr.Empty())
        {
            bgr.Dispose();
            bgr = default!;
            return false;
        }

        return true;
    }

    public void Dispose()
    {
        _cap.Release();
        _cap.Dispose();
    }
}
