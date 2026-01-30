using System.Drawing;
using OpenCvSharp;

namespace PoseIntegrator.Vision.Geometry;

public static class OpenCvConversions
{
    public static Rectangle ToDrawingRect(this OpenCvSharp.Rect r)
        => new Rectangle(r.X, r.Y, r.Width, r.Height);
}
