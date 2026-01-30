using PoseIntegrator.Vision.Models;

namespace PoseIntegrator.Vision.Abstractions;

public interface IPolygonExtractor
{
    PhysicsPolygonsResult Extract(SegmentationResult seg);
}
