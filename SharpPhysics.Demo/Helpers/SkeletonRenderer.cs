using System.Numerics;
using SharpPhysics.Demo.Integration;
using SFML.Graphics;
using SharpPhysics.Rendering;

namespace SharpPhysics.Demo.Helpers;

/// <summary>
/// Helper class for rendering skeleton overlays from pose detection.
/// Uses the Renderer's primitive drawing methods.
/// </summary>
public static class SkeletonRenderer
{
    // Default colors for different body parts
    public static readonly Color FaceColor = new(255, 255, 0, 200);      // Yellow for face
    public static readonly Color TorsoColor = new(0, 255, 255, 200);     // Cyan for torso
    public static readonly Color LeftArmColor = new(0, 255, 0, 200);     // Green for left arm
    public static readonly Color RightArmColor = new(255, 0, 0, 200);    // Red for right arm
    public static readonly Color LeftLegColor = new(0, 200, 100, 200);   // Teal for left leg
    public static readonly Color RightLegColor = new(200, 100, 0, 200);  // Orange for right leg

    /// <summary>
    /// Draws the full skeleton from a PersonColliderBridge.
    /// </summary>
    /// <param name="renderer">The renderer to draw with.</param>
    /// <param name="bridge">The person collider bridge containing skeleton data.</param>
    /// <param name="lineThickness">Thickness of skeleton lines. Default 3.</param>
    /// <param name="confidenceThreshold">Minimum confidence to draw a keypoint. Default 0.3.</param>
    public static void DrawSkeleton(
        Renderer renderer, 
        PersonColliderBridge? bridge, 
        float lineThickness = 3f,
        float confidenceThreshold = 0.3f)
    {
        if (bridge == null) return;

        // Try to get full skeleton first
        var allSkeletons = bridge.GetAllSkeletons();
        if (!allSkeletons.Any()) return;
        var connections = bridge.GetSkeletonConnections();
        foreach (var s in allSkeletons)
        {
            DrawFullSkeleton(renderer, s.Keypoints, s.Confidences,
                connections, confidenceThreshold);
        }

    }

    /// <summary>
    /// Draws the full 17-keypoint COCO skeleton.
    /// </summary>
    public static void DrawFullSkeleton(
        Renderer renderer,
        Vector2[] keypoints, 
        float[] confidences, 
        (int, int)[] connections,
        float confidenceThreshold = 0.3f)
    {
        // Draw skeleton connections
        foreach (var (idx1, idx2) in connections)
        {
            if (idx1 >= keypoints.Length || idx2 >= keypoints.Length)
                continue;

            if (confidences[idx1] < confidenceThreshold || confidences[idx2] < confidenceThreshold)
                continue;

            var pt1 = keypoints[idx1];
            var pt2 = keypoints[idx2];

            // Choose color based on body part
            Color lineColor = GetConnectionColor(idx1, idx2);

            renderer.DrawLine(pt1, pt2, lineColor);
        }

        // Draw keypoint circles
        for (int i = 0; i < keypoints.Length; i++)
        {
            if (confidences[i] < confidenceThreshold)
                continue;

            var pt = keypoints[i];
            Color circleColor = GetKeypointColor(i);
            float radius = GetKeypointRadius(i);

            renderer.DrawCircle(pt, radius, circleColor);
        }
    }

    /// <summary>
    /// Gets the color for a skeleton connection based on body part.
    /// </summary>
    public static Color GetConnectionColor(int idx1, int idx2)
    {
        // Face connections (0-4)
        if (idx1 <= 4 && idx2 <= 4) return FaceColor;

        // Left arm (5, 7, 9)
        if ((idx1 == 5 && idx2 == 7) || (idx1 == 7 && idx2 == 9)) return LeftArmColor;

        // Right arm (6, 8, 10)
        if ((idx1 == 6 && idx2 == 8) || (idx1 == 8 && idx2 == 10)) return RightArmColor;

        // Left leg (11, 13, 15)
        if ((idx1 == 11 && idx2 == 13) || (idx1 == 13 && idx2 == 15)) return LeftLegColor;

        // Right leg (12, 14, 16)
        if ((idx1 == 12 && idx2 == 14) || (idx1 == 14 && idx2 == 16)) return RightLegColor;

        // Torso (shoulders, hips, shoulder-hip connections)
        return TorsoColor;
    }

    /// <summary>
    /// Gets the color for a keypoint based on its index.
    /// </summary>
    public static Color GetKeypointColor(int idx)
    {
        return idx switch
        {
            0 => new Color(255, 255, 0, 255),    // Nose - yellow
            1 or 2 => new Color(255, 200, 0, 255), // Eyes - orange-yellow
            3 or 4 => new Color(255, 150, 0, 255), // Ears - orange
            5 or 7 or 9 => new Color(0, 255, 0, 255),  // Left arm - green
            6 or 8 or 10 => new Color(255, 0, 0, 255), // Right arm - red
            11 or 13 or 15 => new Color(0, 200, 100, 255), // Left leg - teal
            12 or 14 or 16 => new Color(200, 100, 0, 255), // Right leg - orange
            _ => new Color(255, 255, 255, 255)    // Default - white
        };
    }

    /// <summary>
    /// Gets the radius for a keypoint based on its type.
    /// </summary>
    public static float GetKeypointRadius(int idx)
    {
        return idx switch
        {
            0 => 8f,      // Nose - larger
            1 or 2 => 5f, // Eyes - smaller
            3 or 4 => 5f, // Ears - smaller
            5 or 6 => 7f, // Shoulders - medium
            7 or 8 => 6f, // Elbows - medium
            9 or 10 => 8f, // Wrists - larger (hands)
            11 or 12 => 7f, // Hips - medium
            13 or 14 => 6f, // Knees - medium
            15 or 16 => 6f, // Ankles - medium
            _ => 5f
        };
    }
}
