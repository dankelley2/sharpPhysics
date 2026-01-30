namespace PoseIntegrator.Vision.Models;

/// <summary>
/// Mask is 0..255 binary (or near-binary) in row-major order.
/// </summary>
public sealed record SegmentationResult(
    int Width,
    int Height,
    float Confidence,
    byte[] Mask,
    long TimestampMs
);
