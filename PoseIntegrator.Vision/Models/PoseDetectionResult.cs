using System.Numerics;

namespace PoseIntegrator.Vision.Models;

/// <summary>
/// Represents a single body keypoint (joint).
/// </summary>
public sealed class Keypoint
{
    /// <summary>
    /// Keypoint name (e.g., "nose", "left_shoulder", etc.).
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Normalized X coordinate (0-1 range).
    /// </summary>
    public float X { get; }

    /// <summary>
    /// Normalized Y coordinate (0-1 range).
    /// </summary>
    public float Y { get; }

    /// <summary>
    /// Confidence score (0-1 range).
    /// </summary>
    public float Confidence { get; }

    /// <summary>
    /// Visibility score (0-1 range). Higher means more visible.
    /// </summary>
    public float Visibility { get; }

    public Keypoint(string name, float x, float y, float confidence, float visibility = 1.0f)
    {
        Name = name;
        X = x;
        Y = y;
        Confidence = confidence;
        Visibility = visibility;
    }

    public Vector2 Position => new Vector2(X, Y);
}

/// <summary>
/// Represents a single detected person with their skeleton keypoints.
/// </summary>
public sealed class DetectedPerson
{
    /// <summary>
    /// Unique ID for tracking this person across frames.
    /// </summary>
    public int PersonId { get; }

    /// <summary>
    /// Detected keypoints (17 COCO keypoints) for this person.
    /// </summary>
    public IReadOnlyList<Keypoint> Keypoints { get; }

    /// <summary>
    /// Detection confidence for this person (0-1).
    /// </summary>
    public float Confidence { get; }

    /// <summary>
    /// Bounding box center X (normalized 0-1).
    /// </summary>
    public float BboxCenterX { get; }

    /// <summary>
    /// Bounding box center Y (normalized 0-1).
    /// </summary>
    public float BboxCenterY { get; }

    /// <summary>
    /// Bounding box width (normalized 0-1).
    /// </summary>
    public float BboxWidth { get; }

    /// <summary>
    /// Bounding box height (normalized 0-1).
    /// </summary>
    public float BboxHeight { get; }

    public DetectedPerson(
        int personId,
        IReadOnlyList<Keypoint> keypoints,
        float confidence,
        float bboxCenterX = 0,
        float bboxCenterY = 0,
        float bboxWidth = 0,
        float bboxHeight = 0)
    {
        PersonId = personId;
        Keypoints = keypoints;
        Confidence = confidence;
        BboxCenterX = bboxCenterX;
        BboxCenterY = bboxCenterY;
        BboxWidth = bboxWidth;
        BboxHeight = bboxHeight;
    }
}

/// <summary>
/// Result of pose detection on a single frame.
/// Supports multiple detected people.
/// </summary>
public sealed class PoseDetectionResult
{
    /// <summary>
    /// All detected people in this frame.
    /// </summary>
    public IReadOnlyList<DetectedPerson> People { get; }

    /// <summary>
    /// Detected keypoints (joints) for the first/primary person.
    /// For backward compatibility with single-person code.
    /// </summary>
    public IReadOnlyList<Keypoint> Keypoints => People.Count > 0 ? People[0].Keypoints : Array.Empty<Keypoint>();

    /// <summary>
    /// Overall detection confidence for the first/primary person (0-1).
    /// For backward compatibility with single-person code.
    /// </summary>
    public float Confidence => People.Count > 0 ? People[0].Confidence : 0f;

    /// <summary>
    /// Timestamp in milliseconds (UTC).
    /// </summary>
    public long TimestampMs { get; }

    /// <summary>
    /// Whether at least one person was detected.
    /// </summary>
    public bool PersonDetected => People.Count > 0;

    /// <summary>
    /// Number of people detected in this frame.
    /// </summary>
    public int PersonCount => People.Count;

    public PoseDetectionResult(IReadOnlyList<DetectedPerson> people, long timestampMs)
    {
        People = people;
        TimestampMs = timestampMs;
    }

    /// <summary>
    /// Legacy constructor for single-person results.
    /// Creates a result with a single person from keypoints.
    /// </summary>
    public PoseDetectionResult(IReadOnlyList<Keypoint> keypoints, float confidence, long timestampMs)
    {
        if (keypoints.Count > 0 && confidence > 0)
        {
            People = new[] { new DetectedPerson(0, keypoints, confidence) };
        }
        else
        {
            People = Array.Empty<DetectedPerson>();
        }
        TimestampMs = timestampMs;
    }

    /// <summary>
    /// Creates an empty result (no person detected).
    /// </summary>
    public static PoseDetectionResult Empty(long timestampMs) =>
        new PoseDetectionResult(Array.Empty<DetectedPerson>(), timestampMs);
}

/// <summary>
/// Defines skeleton connections for visualization.
/// </summary>
public static class PoseConnections
{
    /// <summary>
    /// MediaPipe BlazePose keypoint indices (33 keypoints).
    /// </summary>
    public enum BlazePoseKeypoint
    {
        Nose = 0,
        LeftEyeInner = 1,
        LeftEye = 2,
        LeftEyeOuter = 3,
        RightEyeInner = 4,
        RightEye = 5,
        RightEyeOuter = 6,
        LeftEar = 7,
        RightEar = 8,
        MouthLeft = 9,
        MouthRight = 10,
        LeftShoulder = 11,
        RightShoulder = 12,
        LeftElbow = 13,
        RightElbow = 14,
        LeftWrist = 15,
        RightWrist = 16,
        LeftPinky = 17,
        RightPinky = 18,
        LeftIndex = 19,
        RightIndex = 20,
        LeftThumb = 21,
        RightThumb = 22,
        LeftHip = 23,
        RightHip = 24,
        LeftKnee = 25,
        RightKnee = 26,
        LeftAnkle = 27,
        RightAnkle = 28,
        LeftHeel = 29,
        RightHeel = 30,
        LeftFootIndex = 31,
        RightFootIndex = 32
    }

    /// <summary>
    /// COCO keypoint indices (17 keypoints) - used by YOLOv8, RTMPose, MoveNet.
    /// </summary>
    public enum CocoKeypoint
    {
        Nose = 0,
        LeftEye = 1,
        RightEye = 2,
        LeftEar = 3,
        RightEar = 4,
        LeftShoulder = 5,
        RightShoulder = 6,
        LeftElbow = 7,
        RightElbow = 8,
        LeftWrist = 9,
        RightWrist = 10,
        LeftHip = 11,
        RightHip = 12,
        LeftKnee = 13,
        RightKnee = 14,
        LeftAnkle = 15,
        RightAnkle = 16
    }

    /// <summary>
    /// COCO skeleton connections (17-point format for YOLOv8, RTMPose, MoveNet).
    /// </summary>
    public static readonly (int, int)[] CocoConnections = new[]
    {
        // Face
        (0, 1), (0, 2), // Nose to eyes
        (1, 3), (2, 4), // Eyes to ears

        // Torso
        (5, 6),   // Shoulders
        (5, 11), (6, 12), // Shoulders to hips
        (11, 12), // Hips

        // Left arm
        (5, 7), (7, 9),  // Shoulder -> elbow -> wrist

        // Right arm
        (6, 8), (8, 10), // Shoulder -> elbow -> wrist

        // Left leg
        (11, 13), (13, 15), // Hip -> knee -> ankle

        // Right leg
        (12, 14), (14, 16)  // Hip -> knee -> ankle
    };

    /// <summary>
    /// BlazePose skeleton connections (33-point format).
    /// </summary>
    public static readonly (int, int)[] Connections = new[]
    {
        // Face
        (0, 1), (1, 2), (2, 3), (0, 4), (4, 5), (5, 6), // Eyes
        (2, 7), (5, 8), (9, 10), // Ears and mouth

        // Torso
        (11, 12), (11, 23), (12, 24), (23, 24), // Shoulders to hips

        // Left arm
        (11, 13), (13, 15), (15, 17), (15, 19), (15, 21), // Shoulder to hand

        // Right arm
        (12, 14), (14, 16), (16, 18), (16, 20), (16, 22), // Shoulder to hand

        // Left leg
        (23, 25), (25, 27), (27, 29), (27, 31), // Hip to foot

        // Right leg
        (24, 26), (26, 28), (28, 30), (28, 32) // Hip to foot
    };

    /// <summary>
    /// Gets appropriate connections based on keypoint count.
    /// </summary>
    public static (int, int)[] GetConnections(int keypointCount)
    {
        return keypointCount == 17 ? CocoConnections : Connections;
    }
}
