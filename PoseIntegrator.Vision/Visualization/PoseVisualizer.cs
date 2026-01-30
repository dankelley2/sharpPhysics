using System.Numerics;
using OpenCvSharp;
using PoseIntegrator.Vision.Models;

namespace PoseIntegrator.Vision.Visualization;

/// <summary>
/// Visualizes pose detection results with skeleton overlay.
/// </summary>
public sealed class PoseVisualizer : IDisposable
{
    private readonly string _windowName;
    private Mat? _displayFrame;

    public PoseVisualizer(string windowName = "Pose Detection")
    {
        _windowName = windowName;
        Cv2.NamedWindow(_windowName, WindowFlags.Normal);
        Cv2.ResizeWindow(_windowName, 640, 480);
    }

    /// <summary>
    /// Visualizes pose detection result on the frame.
    /// </summary>
    public void Visualize(Mat frame, PoseDetectionResult poseResult)
    {
        if (frame == null || frame.Empty())
            return;

        // Clone frame for drawing
        _displayFrame?.Dispose();
        _displayFrame = frame.Clone();

        if (poseResult.PersonDetected)
        {
            DrawSkeleton(_displayFrame, poseResult.Keypoints);
            DrawKeypoints(_displayFrame, poseResult.Keypoints);
        }

        // Show confidence and keypoint count
        var text = $"Confidence: {poseResult.Confidence:F2} | Keypoints: {poseResult.Keypoints.Count}";
        Cv2.PutText(_displayFrame, text, new OpenCvSharp.Point(10, 30),
            HersheyFonts.HersheySimplex, 0.7, Scalar.White, 2);

        Cv2.ImShow(_windowName, _displayFrame);
    }

    private void DrawSkeleton(Mat frame, IReadOnlyList<Keypoint> keypoints)
    {
        if (keypoints.Count == 0)
            return;

        int width = frame.Width;
        int height = frame.Height;

        // Select appropriate connections based on keypoint count
        var connections = PoseConnections.GetConnections(keypoints.Count);

        // Draw connections
        foreach (var (idx1, idx2) in connections)
        {
            if (idx1 >= keypoints.Count || idx2 >= keypoints.Count)
                continue;

            var kp1 = keypoints[idx1];
            var kp2 = keypoints[idx2];

            if (kp1.Confidence < 0.3f || kp2.Confidence < 0.3f)
                continue;

            var pt1 = new OpenCvSharp.Point(
                (int)(kp1.X * width),
                (int)(kp1.Y * height)
            );

            var pt2 = new OpenCvSharp.Point(
                (int)(kp2.X * width),
                (int)(kp2.Y * height)
            );

            // Color based on visibility
            var color = GetBoneColor(kp1, kp2);
            Cv2.Line(frame, pt1, pt2, color, 2);
        }
    }

    private void DrawKeypoints(Mat frame, IReadOnlyList<Keypoint> keypoints)
    {
        int width = frame.Width;
        int height = frame.Height;

        foreach (var kp in keypoints)
        {
            if (kp.Confidence < 0.3f)
                continue;

            var pt = new OpenCvSharp.Point(
                (int)(kp.X * width),
                (int)(kp.Y * height)
            );

            // Color based on confidence
            var color = GetKeypointColor(kp);
            Cv2.Circle(frame, pt, 4, color, -1);
            Cv2.Circle(frame, pt, 5, Scalar.White, 1);
        }
    }

    private Scalar GetBoneColor(Keypoint kp1, Keypoint kp2)
    {
        float avgVisibility = (kp1.Visibility + kp2.Visibility) / 2f;
        
        if (avgVisibility > 0.8f)
            return new Scalar(0, 255, 0); // Green - highly visible
        else if (avgVisibility > 0.5f)
            return new Scalar(0, 255, 255); // Yellow - moderately visible
        else
            return new Scalar(0, 128, 255); // Orange - low visibility
    }

    private Scalar GetKeypointColor(Keypoint kp)
    {
        if (kp.Confidence > 0.9f)
            return new Scalar(0, 255, 0); // Green - high confidence
        else if (kp.Confidence > 0.7f)
            return new Scalar(0, 255, 255); // Yellow - medium confidence
        else
            return new Scalar(0, 128, 255); // Orange - low confidence
    }

    public void Dispose()
    {
        _displayFrame?.Dispose();
        Cv2.DestroyWindow(_windowName);
    }
}
