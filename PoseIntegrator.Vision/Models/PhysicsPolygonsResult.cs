using System.Numerics;
using System.Drawing;

namespace PoseIntegrator.Vision.Models;

public sealed record PhysicsPolygonsResult(
    IReadOnlyList<IReadOnlyList<Vector2>> ConvexPolygons,
    Rectangle BoundingBoxPixels,
    float Confidence,
    long TimestampMs
);
