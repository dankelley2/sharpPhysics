using OpenCvSharp;
using PoseIntegrator.Vision;
using PoseIntegrator.Vision.Segmentation;
using PoseIntegrator.Vision.Visualization;
using PoseIntegrator.Vision.FrameSources;
using PoseIntegrator.Vision.Geometry;
using PoseIntegrator.Vision.PoseDetection;
using PoseIntegrator.Vision.Models;

namespace PoseIntegrator.Demo;

static class Program
{

    static void Main(string[] args)
    {
        // Model paths - relative to project root
        string POSE_MODEL = "/Users/danielkelley/PoseIntegrator/src/PoseIntegrator.Demo/modles/yolo26s_pose.onnx";
        Console.WriteLine(AppContext.BaseDirectory);
        using var camera = new OpenCvCameraFrameSource(0, 640, 480, 30);
        //using var camera = new MjpegCameraFrameSource("http://192.168.1.161:8080", 5, true);
        using var poseVisualizer = new PoseVisualizer("Pose Detection");

        // Initialize pose detector if model exists
        IPoseDetector? yoloPoseDetector = null;

        yoloPoseDetector = new YoloV26PoseDetector(POSE_MODEL, useGpu: true);

        Console.WriteLine($"\nStarting...");

        bool firstFrame = true;

        while (true)
        {
            if (!camera.TryGetFrame(out var frame, out var timestamp))
            {
                Thread.Sleep(5);
                continue;
            }

            try
            {
                if (yoloPoseDetector == null)
                {
                    throw new InvalidOperationException("Pose detector not initialized.");
                }
                PoseDetectionResult poseResult;

                poseResult = yoloPoseDetector.Detect(frame, timestamp);

                poseVisualizer.Visualize(frame, poseResult);

                // Process window events - required for display on macOS
                var key = Cv2.WaitKey(1);
                if (key == 'q' || key == 27) // 'q' or ESC
                    break;

                if (firstFrame)
                {
                    // Bring window to front on first frame
                    Cv2.SetWindowProperty("Pose Detection", WindowPropertyFlags.Topmost, 1);
                    Cv2.SetWindowProperty("Pose Detection", WindowPropertyFlags.Topmost, 0);
                    firstFrame = false;
                }
            }
            finally
            {
                frame.Dispose();
            }
        }

    }
}
