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
    private const string APP_FOLDER = "SharpPhysics.Demo";
    private const string SETTINGS_FILENAME = "gamesettings.json";

    /// <summary>
    /// Gets the full path to the settings file in the user's local app data directory.
    /// Cross-platform: Windows (%LOCALAPPDATA%), macOS (~/Library/Application Support), Linux (~/.local/share)
    /// </summary>
    private static string SettingsFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        APP_FOLDER,
        SETTINGS_FILENAME
    );

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
    /// Requested camera device resolution width (may not be honored by all cameras/backends).
    /// </summary>
    public int CameraDeviceResolutionX { get; set; } = 640;

    /// <summary>
    /// Requested camera device resolution height (may not be honored by all cameras/backends).
    /// </summary>
    public int CameraDeviceResolutionY { get; set; } = 480;

    /// <summary>
    /// Requested camera device FPS (only applies to local cameras, not MJPEG streams).
    /// </summary>
    public int CameraDeviceFps { get; set; } = 30;

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
    /// Settings are stored in the user's local app data directory.
    /// </summary>
    public static GameSettings Load()
    {
        var settingsPath = SettingsFilePath;
        try
        {
            if (File.Exists(settingsPath))
            {
                var json = File.ReadAllText(settingsPath);
                var settings = JsonSerializer.Deserialize<GameSettings>(json, GetJsonOptions());
                if (settings != null)
                {
                    Console.WriteLine($"[Settings] Loaded from {settingsPath}");
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Settings] Failed to load: {ex.Message}");
        }

        Console.WriteLine($"[Settings] Using default settings (file: {settingsPath})");
        return new GameSettings();
    }

    /// <summary>
    /// Saves the current settings to the JSON file.
    /// Settings are stored in the user's local app data directory.
    /// </summary>
    public void Save()
    {
        var settingsPath = SettingsFilePath;
        try
        {
            // Ensure the directory exists
            var directory = Path.GetDirectoryName(settingsPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(this, GetJsonOptions());
            File.WriteAllText(settingsPath, json);
            Console.WriteLine($"[Settings] Saved to {settingsPath}");
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
