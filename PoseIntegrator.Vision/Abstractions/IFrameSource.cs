using OpenCvSharp;

namespace PoseIntegrator.Vision.Abstractions;

public interface IFrameSource : IDisposable
{
    bool TryGetFrame(out Mat bgr, out long timestampMs);
}
