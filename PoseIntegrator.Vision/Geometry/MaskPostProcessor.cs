using OpenCvSharp;

namespace PoseIntegrator.Vision.Geometry;

public static class MaskPostProcessor
{
    public static Mat ToCleanBinaryMask(byte[] mask, int w, int h)
    {
        var mat = new Mat(h, w, MatType.CV_8UC1);
        mat.SetArray(mask);

        // Ensure binary
        Cv2.Threshold(mat, mat, 127, 255, ThresholdTypes.Binary);

        // Basic cleanup (kept minimal)
        var k = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(5, 5));
        Cv2.MorphologyEx(mat, mat, MorphTypes.Open, k);
        Cv2.MorphologyEx(mat, mat, MorphTypes.Close, k);

        return mat;
    }
}
