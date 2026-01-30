using OpenCvSharp;
using PoseIntegrator.Vision.Models;

namespace PoseIntegrator.Vision.Abstractions;

public interface IPersonSegmenter : IDisposable
{
    SegmentationResult Segment(Mat bgr, long timestampMs);
}
