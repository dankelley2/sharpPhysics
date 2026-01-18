# SharpPhysics Integration Guide

This document provides instructions for integrating **ProjectorSegmentation.Vision** with **SharpPhysics** to create a person silhouette as a physics collider.

## Overview

The ProjectorSegmentation library detects a person via webcam and outputs convex polygon(s) representing their silhouette. These polygons can be used as static colliders in SharpPhysics, allowing physics objects to interact with a real person's shape.

---

## Required Namespaces

### ProjectorSegmentation.Vision
```csharp
using ProjectorSegmentation.Vision;                    // PersonDetector, PersonDetectedEventArgs
using ProjectorSegmentation.Vision.Segmentation;       // OnnxSegmentationOptions
using System.Numerics;                                 // Vector2 (polygon output type)
```

### SharpPhysics
```csharp
using SharpPhysics;                // PhysicsWorld, RigidBody
using SharpPhysics.Colliders;      // PolygonCollider
using SharpPhysics.Math;           // Vector2f (physics vector type)
```

---

## Type Conversions

### Vector Conversion

ProjectorSegmentation outputs `System.Numerics.Vector2` (normalized 0-1).  
SharpPhysics uses `SharpPhysics.Math.Vector2f`.

```csharp
// Convert from ProjectorSegmentation to SharpPhysics coordinates
System.Numerics.Vector2 input;   // normalized 0-1
float worldWidth, worldHeight;   // your physics world dimensions

SharpPhysics.Math.Vector2f output = new Vector2f(
    input.X * worldWidth,
    input.Y * worldHeight
);
```

### Coordinate System Notes

| Library | Origin | X-Axis | Y-Axis |
|---------|--------|--------|--------|
| ProjectorSegmentation | Top-left | Right (0→1) | Down (0→1) |
| SharpPhysics | Configurable | Right | Down (typically) |

If SharpPhysics uses Y-up, flip the Y coordinate:
```csharp
new Vector2f(input.X * worldWidth, worldHeight - (input.Y * worldHeight))
```

---

## PersonDetector API Reference

### Constructor
```csharp
public PersonDetector(string modelPath, OnnxSegmentationOptions? options = null)
```

### Start Detection
```csharp
// With built-in camera
void Start(int cameraIndex = 0, int width = 640, int height = 480, int? fps = 30)

// With custom frame source
void Start(IFrameSource frameSource)
```

### Stop Detection
```csharp
void Stop()
```

### Events

| Event | Args | Description |
|-------|------|-------------|
| `OnPersonDetected` | `PersonDetectedEventArgs` | Fired when person detected (background thread) |
| `OnError` | `Exception` | Fired on processing errors |
| `OnFrameProcessed` | `FrameProcessedEventArgs` | Fired for every frame (debugging) |

### PersonDetectedEventArgs Properties

| Property | Type | Description |
|----------|------|-------------|
| `Polygons` | `IReadOnlyList<IReadOnlyList<Vector2>>` | Convex polygon(s), normalized 0-1 |
| `BoundingBox` | `System.Drawing.Rectangle` | Pixel-space bounding box |
| `Confidence` | `float` | Detection confidence 0-1 |
| `TimestampMs` | `long` | UTC timestamp in milliseconds |

---

## Integration Implementation

### 1. Create Integration Class

```csharp
using System.Numerics;
using ProjectorSegmentation.Vision;
using ProjectorSegmentation.Vision.Segmentation;
using SharpPhysics;
using SharpPhysics.Colliders;
using SharpPhysics.Math;

namespace SharpPhysics.Integration;

/// <summary>
/// Bridges ProjectorSegmentation person detection with SharpPhysics world.
/// Creates/updates a static RigidBody representing the detected person silhouette.
/// </summary>
public sealed class PersonColliderBridge : IDisposable
{
    private readonly PhysicsWorld _world;
    private readonly PersonDetector _detector;
    private readonly float _worldWidth;
    private readonly float _worldHeight;
    private readonly bool _flipY;
    
    private RigidBody? _personBody;
    private readonly object _syncLock = new();

    /// <summary>
    /// Creates a new PersonColliderBridge.
    /// </summary>
    /// <param name="world">The SharpPhysics world to add the person body to.</param>
    /// <param name="worldWidth">Physics world width (for coordinate scaling).</param>
    /// <param name="worldHeight">Physics world height (for coordinate scaling).</param>
    /// <param name="modelPath">Path to the ONNX segmentation model.</param>
    /// <param name="flipY">Set true if SharpPhysics uses Y-up coordinates.</param>
    /// <param name="options">Optional ONNX model configuration.</param>
    public PersonColliderBridge(
        PhysicsWorld world,
        float worldWidth,
        float worldHeight,
        string modelPath,
        bool flipY = false,
        OnnxSegmentationOptions? options = null)
    {
        _world = world;
        _worldWidth = worldWidth;
        _worldHeight = worldHeight;
        _flipY = flipY;
        
        _detector = new PersonDetector(modelPath, options);
        _detector.OnPersonDetected += HandlePersonDetected;
        _detector.OnError += (_, ex) => OnError?.Invoke(this, ex);
    }

    /// <summary>
    /// Fired when an error occurs in detection.
    /// </summary>
    public event EventHandler<Exception>? OnError;

    /// <summary>
    /// Fired after the person body is updated in the physics world.
    /// </summary>
    public event EventHandler<RigidBody>? OnPersonBodyUpdated;

    /// <summary>
    /// The current person RigidBody (null if no person detected).
    /// </summary>
    public RigidBody? PersonBody
    {
        get { lock (_syncLock) return _personBody; }
    }

    /// <summary>
    /// Start detection with the specified camera.
    /// </summary>
    public void Start(int cameraIndex = 0, int width = 640, int height = 480)
    {
        _detector.Start(cameraIndex, width, height);
    }

    /// <summary>
    /// Stop detection and remove person body from world.
    /// </summary>
    public void Stop()
    {
        _detector.Stop();
        
        lock (_syncLock)
        {
            if (_personBody != null)
            {
                _world.RemoveBody(_personBody);
                _personBody = null;
            }
        }
    }

    private void HandlePersonDetected(object? sender, PersonDetectedEventArgs e)
    {
        if (e.Polygons.Count == 0) return;

        // Use first polygon (largest/primary silhouette)
        var polygon = e.Polygons[0];
        
        // Convert to physics coordinates
        var physicsVerts = ConvertToPhysicsCoords(polygon);
        
        // Calculate centroid for body position
        var centroid = CalculateCentroid(physicsVerts);
        
        // Convert to local coordinates (relative to centroid)
        var localVerts = physicsVerts
            .Select(v => new Vector2f(v.X - centroid.X, v.Y - centroid.Y))
            .ToArray();

        lock (_syncLock)
        {
            // Remove existing body
            if (_personBody != null)
            {
                _world.RemoveBody(_personBody);
            }

            // Create new static body
            _personBody = new RigidBody(
                position: centroid,
                rotation: 0f,
                isStatic: true
            );
            
            _personBody.AddCollider(new PolygonCollider(localVerts));
            _world.AddBody(_personBody);
        }
        
        OnPersonBodyUpdated?.Invoke(this, _personBody);
    }

    private Vector2f[] ConvertToPhysicsCoords(IReadOnlyList<Vector2> polygon)
    {
        return polygon.Select(p =>
        {
            float x = p.X * _worldWidth;
            float y = _flipY 
                ? _worldHeight - (p.Y * _worldHeight)
                : p.Y * _worldHeight;
            return new Vector2f(x, y);
        }).ToArray();
    }

    private static Vector2f CalculateCentroid(Vector2f[] vertices)
    {
        float x = 0, y = 0;
        foreach (var v in vertices)
        {
            x += v.X;
            y += v.Y;
        }
        return new Vector2f(x / vertices.Length, y / vertices.Length);
    }

    public void Dispose()
    {
        Stop();
        _detector.Dispose();
    }
}
```

### 2. Usage Example

```csharp
// Initialize physics
var world = new PhysicsWorld(new Vector2f(0, 9.8f));

// Initialize person detection bridge
using var personBridge = new PersonColliderBridge(
    world: world,
    worldWidth: 800f,
    worldHeight: 600f,
    modelPath: "models/person-segmentation.onnx",
    flipY: false  // true if Y-up coordinate system
);

personBridge.OnError += (s, ex) => Console.WriteLine($"Error: {ex.Message}");
personBridge.OnPersonBodyUpdated += (s, body) => Console.WriteLine("Person updated");

// Start detection
personBridge.Start(cameraIndex: 0, width: 640, height: 480);

// Game loop
while (running)
{
    float dt = GetDeltaTime();
    
    // Physics updates automatically include person silhouette
    world.Step(dt);
    
    // Render world...
}

// Cleanup
personBridge.Stop();
```

---

## Threading Considerations

⚠️ **IMPORTANT**: `OnPersonDetected` fires on a **background thread**.

### Option A: Lock when modifying world (shown above)
```csharp
lock (_syncLock)
{
    _world.RemoveBody(_personBody);
    _world.AddBody(newBody);
}
```

### Option B: Queue updates for main thread
```csharp
private ConcurrentQueue<Action> _pendingUpdates = new();

private void HandlePersonDetected(...)
{
    var verts = ConvertPolygon(e.Polygons[0]);
    _pendingUpdates.Enqueue(() => UpdatePersonBody(verts));
}

// In game loop:
while (_pendingUpdates.TryDequeue(out var action))
    action();
```

---

## Model Setup

The ONNX model must be downloaded before use. The library can auto-download on first run.

**Model**: FCN ResNet-50 from ONNX Model Zoo  
**URL**: `https://github.com/onnx/models/raw/main/validated/vision/object_detection_segmentation/fcn/model/fcn-resnet50-12.onnx`  
**Size**: ~134 MB  
**Path**: `models/person-segmentation.onnx` (relative to working directory)

### OnnxSegmentationOptions (FCN ResNet-50)

```csharp
var options = new OnnxSegmentationOptions
{
    ModelPath = "models/person-segmentation.onnx",
    InputTensorName = "input",
    OutputTensorName = "out",
    InputWidth = 520,
    InputHeight = 520,
    Threshold = 0.5f
};
```

---

## Performance Notes

| Metric | Typical Value |
|--------|---------------|
| Detection FPS | ~1-2 FPS (CPU), 10+ FPS (GPU) |
| Polygon points | 10-30 vertices |
| Latency | ~500-1000ms |

For smoother physics, consider:
- Interpolating between polygon updates
- Using a lower detection resolution
- Running ONNX with GPU acceleration (DirectML/CUDA)

---

## File Checklist

Files needed in SharpPhysics project:

- [ ] Reference `ProjectorSegmentation.Vision.dll`
- [ ] Copy/implement `PersonColliderBridge.cs` 
- [ ] Ensure `models/person-segmentation.onnx` is accessible
- [ ] Add NuGet: `OpenCvSharp4.runtime.win` (Windows only)

---

## Quick Reference

```csharp
// Minimal integration
using var detector = new PersonDetector("model.onnx");
detector.OnPersonDetected += (s, e) =>
{
    foreach (var polygon in e.Polygons)
    {
        Vector2f[] verts = polygon
            .Select(p => new Vector2f(p.X * worldW, p.Y * worldH))
            .ToArray();
        // Create PolygonCollider from verts...
    }
};
detector.Start(cameraIndex: 0);
```
