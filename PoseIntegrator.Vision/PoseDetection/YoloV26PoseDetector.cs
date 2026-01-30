using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using PoseIntegrator.Vision.Models;

namespace PoseIntegrator.Vision.PoseDetection;

/// <summary>
/// YOLOv26-Pose detector using ONNX Runtime.
/// NMS-free architecture with 300 pre-filtered detections.
/// Supports multi-person detection.
/// </summary>
public sealed class YoloV26PoseDetector : IPoseDetector
{
    private readonly InferenceSession _session;
    private readonly int _inputWidth;
    private readonly int _inputHeight;
    private readonly float _confidenceThreshold;
    private readonly int _maxPeople;
    private readonly string _inputName;

    /// <summary>
    /// YOLOv26-Pose COCO keypoint names (17 keypoints).
    /// </summary>
    private static readonly string[] KeypointNames = new[]
    {
        "nose",
        "left_eye", "right_eye",
        "left_ear", "right_ear",
        "left_shoulder", "right_shoulder",
        "left_elbow", "right_elbow",
        "left_wrist", "right_wrist",
        "left_hip", "right_hip",
        "left_knee", "right_knee",
        "left_ankle", "right_ankle"
    };

    /// <summary>
    /// Creates a new YOLOv26-Pose detector.
    /// </summary>
    /// <param name="modelPath">Path to the ONNX model file.</param>
    /// <param name="inputWidth">Model input width (default 640).</param>
    /// <param name="inputHeight">Model input height (default 640).</param>
    /// <param name="confidenceThreshold">Minimum confidence to include a detection (default 0.5).</param>
    /// <param name="useGpu">Use GPU acceleration if available (default true).</param>
    /// <param name="maxPeople">Maximum number of people to detect (default 10).</param>
    public YoloV26PoseDetector(
        string modelPath,
        int inputWidth = 640,
        int inputHeight = 640,
        float confidenceThreshold = 0.5f,
        bool useGpu = true,
        int maxPeople = 10)
    {
        _inputWidth = inputWidth;
        _inputHeight = inputHeight;
        _confidenceThreshold = confidenceThreshold;
        _maxPeople = maxPeople;

        var options = new SessionOptions();

        if (useGpu)
        {
            try
            {
                options.EnableMemoryPattern = false;
                options.ExecutionMode = ExecutionMode.ORT_SEQUENTIAL;
                options.AppendExecutionProvider_DML(0);
                Console.WriteLine("YOLOv26-Pose: Using DirectML (GPU) acceleration");
            }
            catch
            {
                Console.WriteLine("YOLOv26-Pose: DirectML not available, using CPU");
            }
        }

            _session = new InferenceSession(modelPath, options);

            // Log model info and cache input name
            var inputMeta = _session.InputMetadata.First();
            _inputName = inputMeta.Key;
            Console.WriteLine($"YOLOv26-Pose loaded: Input '{inputMeta.Key}' shape: [{string.Join(", ", inputMeta.Value.Dimensions)}]");
            Console.WriteLine($"YOLOv26-Pose: Multi-person detection enabled (max {_maxPeople} people)");
        }

    public PoseDetectionResult Detect(Mat frame, long timestampMs)
    {
        if (frame == null || frame.Empty())
            return PoseDetectionResult.Empty(timestampMs);

        try
        {
            // Preprocess - YOLO v26 uses simple resize, NOT letterboxing (do_pad: false)
            using var preprocessed = PreprocessFrame(frame, out float scaleX, out float scaleY);

            // Convert to YOLO tensor format (NCHW: batch, channels, height, width)
            var tensor = MatToTensor(preprocessed);

            // Run inference using cached input name
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(_inputName, tensor)
            };

            using var results = _session.Run(inputs);
            var output = results.First().AsTensor<float>();

            // Parse YOLOv26 output format - returns all detected people
            var people = ParseYoloOutputMultiPerson(output, scaleX, scaleY, frame.Width, frame.Height);

            return new PoseDetectionResult(people, timestampMs);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"YOLOv26 Pose detection error: {ex.Message}");
            return PoseDetectionResult.Empty(timestampMs);
        }
    }

    /// <summary>
    /// Preprocesses frame with simple resize for YOLO v26 (no letterboxing, do_pad: false).
    /// </summary>
    private Mat PreprocessFrame(Mat frame, out float scaleX, out float scaleY)
    {
        // Calculate scale factors
        scaleX = (float)frame.Width / _inputWidth;
        scaleY = (float)frame.Height / _inputHeight;

        // Resize frame to input size (no letterboxing)
        using var resized = new Mat();
        Cv2.Resize(frame, resized, new Size(_inputWidth, _inputHeight));

        // Convert BGR to RGB
        var rgb = new Mat();
        Cv2.CvtColor(resized, rgb, ColorConversionCodes.BGR2RGB);

        return rgb;
    }

    /// <summary>
    /// Converts Mat to YOLO tensor format (NCHW, normalized 0-1 range).
    /// Optimized for performance using direct buffer access and channel-first iteration.
    /// </summary>
    private DenseTensor<float> MatToTensor(Mat mat)
    {
        // YOLO expects NCHW format (batch, channels, height, width)
        var tensor = new DenseTensor<float>(new[] { 1, 3, _inputHeight, _inputWidth });

        // Pre-compute normalization factor (multiplication is faster than division)
        const float scale = 1f / 255f;

        // Get direct access to tensor's underlying buffer to avoid indexer overhead
        var tensorSpan = tensor.Buffer.Span;
        int channelSize = _inputHeight * _inputWidth;

        unsafe
        {
            byte* ptr = (byte*)mat.DataPointer;
            int step = (int)mat.Step();

            // Process each channel separately for better cache locality
            // Channel 0 (R) starts at index 0
            // Channel 1 (G) starts at index channelSize
            // Channel 2 (B) starts at index channelSize * 2
            for (int y = 0; y < _inputHeight; y++)
            {
                byte* row = ptr + (y * step);
                int rowOffset = y * _inputWidth;

                for (int x = 0; x < _inputWidth; x++)
                {
                    int srcOffset = x * 3;
                    int dstOffset = rowOffset + x;

                    // Write each channel to contiguous memory regions
                    tensorSpan[dstOffset] = row[srcOffset] * scale;                     // R
                    tensorSpan[channelSize + dstOffset] = row[srcOffset + 1] * scale;   // G
                    tensorSpan[channelSize * 2 + dstOffset] = row[srcOffset + 2] * scale; // B
                }
            }
        }

        return tensor;
    }

    /// <summary>
    /// Parses YOLOv26-Pose output format for multiple people.
    /// Output shape: [1, 300, 57] where 57 = 6 (bbox+conf+class) + 51 (17 keypoints × 3)
    /// </summary>
    private IReadOnlyList<DetectedPerson> ParseYoloOutputMultiPerson(
        Tensor<float> output,
        float scaleX,
        float scaleY,
        int originalWidth,
        int originalHeight)
    {
        var people = new List<DetectedPerson>();
        var dims = output.Dimensions.ToArray();

        if (dims.Length != 3)
            return people;

        // YOLOv26 format: [1, N, 57] where N=300 detections, 57 features per detection
        int numDetections = dims[1];
        int numFeatures = dims[2];

        // Collect all valid detections with their confidence
        var validDetections = new List<(int index, float confidence)>();

        for (int i = 0; i < numDetections; i++)
        {
            float confidence = output[0, i, 4];
            if (confidence > _confidenceThreshold)
            {
                validDetections.Add((i, confidence));
            }
        }

        // Sort by confidence (highest first) and take top N
        validDetections = validDetections
            .OrderByDescending(d => d.confidence)
            .Take(_maxPeople)
            .ToList();

        // Determine if coordinates are in pixels or normalized based on first valid detection
        bool pixelCoords = false;
        if (validDetections.Count > 0)
        {
            int firstIdx = validDetections[0].index;
            float bboxX = output[0, firstIdx, 0];
            float bboxY = output[0, firstIdx, 1];
            pixelCoords = bboxX > 2 || bboxY > 2;
        }

        // Extract keypoints for each detected person
        int personId = 0;
        foreach (var (detectionIdx, confidence) in validDetections)
        {
            var keypoints = ExtractKeypointsForDetection(
                output, detectionIdx, numFeatures,
                scaleX, scaleY, originalWidth, originalHeight,
                pixelCoords, confidence);

            if (keypoints.Count > 0)
            {
                // Extract bounding box info
                float bboxX = output[0, detectionIdx, 0];
                float bboxY = output[0, detectionIdx, 1];
                float bboxW = output[0, detectionIdx, 2];
                float bboxH = output[0, detectionIdx, 3];

                // Normalize bbox if in pixel coordinates
                float bboxCenterXNorm, bboxCenterYNorm, bboxWidthNorm, bboxHeightNorm;
                if (pixelCoords)
                {
                    bboxCenterXNorm = (bboxX * scaleX) / originalWidth;
                    bboxCenterYNorm = (bboxY * scaleY) / originalHeight;
                    bboxWidthNorm = (bboxW * scaleX) / originalWidth;
                    bboxHeightNorm = (bboxH * scaleY) / originalHeight;
                }
                else
                {
                    bboxCenterXNorm = bboxX;
                    bboxCenterYNorm = bboxY;
                    bboxWidthNorm = bboxW;
                    bboxHeightNorm = bboxH;
                }

                people.Add(new DetectedPerson(
                    personId++,
                    keypoints,
                    confidence,
                    Math.Clamp(bboxCenterXNorm, 0f, 1f),
                    Math.Clamp(bboxCenterYNorm, 0f, 1f),
                    Math.Clamp(bboxWidthNorm, 0f, 1f),
                    Math.Clamp(bboxHeightNorm, 0f, 1f)
                ));
            }
        }

        return people;
    }

    /// <summary>
    /// Extracts the 17 COCO keypoints for a single detection.
    /// </summary>
    private List<Keypoint> ExtractKeypointsForDetection(
        Tensor<float> output,
        int detectionIdx,
        int numFeatures,
        float scaleX,
        float scaleY,
        int originalWidth,
        int originalHeight,
        bool pixelCoords,
        float detectionConfidence)
    {
        var keypoints = new List<Keypoint>();

        // YOLOv26 format: [x, y, w, h, confidence, class_id, kp0_x, kp0_y, kp0_vis, ...]
        // Keypoints start at index 6
        const int keypointStartIdx = 6;

        // Extract all 17 keypoints
        for (int kpIdx = 0; kpIdx < 17; kpIdx++)
        {
            int baseIdx = keypointStartIdx + (kpIdx * 3);

            if (baseIdx + 2 >= numFeatures)
                break;

            // Format: [x, y, visibility]
            float rawX = output[0, detectionIdx, baseIdx + 0];
            float rawY = output[0, detectionIdx, baseIdx + 1];
            float rawVisibility = output[0, detectionIdx, baseIdx + 2];

            float xNorm, yNorm;

            if (pixelCoords)
            {
                // Coordinates are in pixels relative to 640x640 input
                xNorm = (rawX * scaleX) / originalWidth;
                yNorm = (rawY * scaleY) / originalHeight;
            }
            else
            {
                // Coordinates are already normalized (0-1)
                xNorm = rawX;
                yNorm = rawY;
            }

            // Clamp to valid range
            xNorm = Math.Clamp(xNorm, 0f, 1f);
            yNorm = Math.Clamp(yNorm, 0f, 1f);

            // Use detection confidence if keypoint is visible (visibility > 0)
            float kpConfidence = rawVisibility > 0.0001f ? detectionConfidence : 0f;

            string name = kpIdx < KeypointNames.Length ? KeypointNames[kpIdx] : $"keypoint_{kpIdx}";
            keypoints.Add(new Keypoint(name, xNorm, yNorm, kpConfidence, rawVisibility));
        }

        return keypoints;
    }

    public void Dispose()
    {
        _session?.Dispose();
    }
}
