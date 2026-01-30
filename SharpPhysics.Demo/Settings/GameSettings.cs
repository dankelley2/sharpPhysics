#nullable enable
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SharpPhysics.Demo.Settings;

/// <summary>
/// Centralized settings for SharpPhysics.Demo application.
/// Supports JSON serialization for persistence.
/// </summary>
public class GameSettings
{
    private static GameSettings? _instance;
    private static readonly object _lock = new();
    private const string SETTINGS_FILE = "gamesettings.json";

    /// <summary>
    /// Gets the singleton instance of GameSettings.
    /// </summary>
    public static GameSettings Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= Load();
                }
            }
            return _instance;
        }
    }

    // ==================== Window Settings ====================

    /// <summary>
    /// Window width in pixels.
    /// </summary>
    public uint WindowWidth { get; set; } = 1280;

    /// <summary>
    /// Window height in pixels.
    /// </summary>
    public uint WindowHeight { get; set; } = 720;

    /// <summary>
    /// Maximum framerate limit (0 = unlimited).
    /// </summary>
    public uint FramerateLimit { get; set; } = 144;

    /// <summary>
    /// Antialiasing level (0, 2, 4, 8).
    /// </summary>
    public uint AntialiasingLevel { get; set; } = 8;

    // ==================== Camera Settings ====================

    /// <summary>
    /// Whether or not to run any camera or pose tracking functionality
    /// Requires PoseIntegrator.Vision DLL and compatible MJPEG stream or
    /// Connected webcam
    /// </summary>
    public bool PoseTrackingEnabled { get; set; } = false;

    /// <summary>
    /// Camera source type: "url" for MJPEG stream, "device" for local camera.
    /// </summary>
    public string CameraSourceType { get; set; } = "url";

    /// <summary>
    /// MJPEG stream URL for IP camera (when CameraSourceType is "url").
    /// </summary>
    public string CameraUrl { get; set; } = "http://192.168.1.161:8080";

    /// <summary>
    /// Local camera device index (when CameraSourceType is "device").
    /// </summary>
    public int CameraDeviceIndex { get; set; } = 0;

    /// <summary>
    /// Camera capture width.
    /// </summary>
    public int CameraWidth { get; set; } = 640;

    /// <summary>
    /// Camera capture height.
    /// </summary>
    public int CameraHeight { get; set; } = 480;

    /// <summary>
    /// Camera capture FPS.
    /// </summary>
    public int CameraFps { get; set; } = 30;

    // ==================== Pose Detection Settings ====================

    /// <summary>
    /// Path to the ONNX pose detection model.
    /// </summary>
    public string ModelPath { get; set; } = "models/yolo26s_pose.onnx";

    /// <summary>
    /// Use GPU acceleration for pose detection.
    /// </summary>
    public bool UseGpu { get; set; } = true;

    /// <summary>
    /// Confidence threshold for pose detection (0.0 - 1.0).
    /// </summary>
    public float ConfidenceThreshold { get; set; } = 0.5f;

    /// <summary>
    /// Maximum number of people to track simultaneously.
    /// </summary>
    public int MaxPeople { get; set; } = 5;

    /// <summary>
    /// Flip X coordinates for mirror mode.
    /// </summary>
    public bool FlipX { get; set; } = true;

    /// <summary>
    /// Flip Y coordinates.
    /// </summary>
    public bool FlipY { get; set; } = false;

    // ==================== Game-Specific Detection Settings ====================

    /// <summary>
    /// Ball radius for DemoGame (Sandbox).
    /// </summary>
    public int SandboxBallRadius { get; set; } = 20;

    /// <summary>
    /// Smoothing factor for DemoGame (Sandbox) (0.0 - 0.95).
    /// </summary>
    public float SandboxSmoothingFactor { get; set; } = 0.5f;

    /// <summary>
    /// Ball radius for BubblePopGame.
    /// </summary>
    public int BubblePopBallRadius { get; set; } = 30;

    /// <summary>
    /// Smoothing factor for BubblePopGame.
    /// </summary>
    public float BubblePopSmoothingFactor { get; set; } = 0.3f;

    /// <summary>
    /// Ball radius for RainCatcherGame.
    /// </summary>
    public int RainCatcherBallRadius { get; set; } = 35;

    /// <summary>
    /// Smoothing factor for RainCatcherGame.
    /// </summary>
    public float RainCatcherSmoothingFactor { get; set; } = 0.4f;

    // ==================== Persistence ====================

    /// <summary>
    /// Loads settings from the JSON file, or creates default settings if not found.
    /// </summary>
    public static GameSettings Load()
    {
        try
        {
            if (File.Exists(SETTINGS_FILE))
            {
                var json = File.ReadAllText(SETTINGS_FILE);
                var settings = JsonSerializer.Deserialize<GameSettings>(json, GetJsonOptions());
                if (settings != null)
                {
                    Console.WriteLine($"[Settings] Loaded from {SETTINGS_FILE}");
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Settings] Failed to load: {ex.Message}");
        }

        Console.WriteLine("[Settings] Using default settings");
        return new GameSettings();
    }

    /// <summary>
    /// Saves the current settings to the JSON file.
    /// </summary>
    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(this, GetJsonOptions());
            File.WriteAllText(SETTINGS_FILE, json);
            Console.WriteLine($"[Settings] Saved to {SETTINGS_FILE}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Settings] Failed to save: {ex.Message}");
        }
    }

    /// <summary>
    /// Reloads settings from file and updates the singleton instance.
    /// </summary>
    public static void Reload()
    {
        lock (_lock)
        {
            _instance = Load();
        }
    }

    private static JsonSerializerOptions GetJsonOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }
}
