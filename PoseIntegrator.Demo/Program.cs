using OpenCvSharp;
using PoseIntegrator.Vision.Visualization;
using PoseIntegrator.Vision.FrameSources;
using PoseIntegrator.Vision.PoseDetection;
using PoseIntegrator.Vision.Models;

namespace PoseIntegrator.Demo;

static class Program
{

    static void Main(string[] args)
    {
        // Model paths - relative to project root
        string POSE_MODEL = @"C:\Users\danth\source\repos\dankelley2\sharpPhysics\SharpPhysics.Demo\models\yolo26s_pose.onnx";
        Console.WriteLine(AppContext.BaseDirectory);
        using var camera = new OpenCvCameraFrameSource(0, 640, 480, 30);
        //using var camera = new MjpegCameraFrameSource("http://192.168.1.161:8080", 5, true);
        using var poseVisualizer = new PoseVisualizer("Pose Detection");

        // Initialize pose detector if model exists
        using var yoloPoseDetector = new YoloV26PoseDetector(POSE_MODEL, useGpu: true);

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
                var poseResult = yoloPoseDetector.Detect(frame, timestamp);

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
