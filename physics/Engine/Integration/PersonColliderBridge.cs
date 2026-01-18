#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using physics.Engine.Objects;
using physics.Engine.Shaders;
using ProjectorSegmentation.Vision;
using ProjectorSegmentation.Vision.Segmentation;

namespace physics.Engine.Integration
{
    /// <summary>
    /// Bridges ProjectorSegmentation person detection with the SharpPhysics engine.
    /// Creates/updates a static PhysicsObject representing the detected person silhouette.
    /// </summary>
    public sealed class PersonColliderBridge : IDisposable
    {
        private readonly float _worldWidth;
        private readonly float _worldHeight;
        private readonly string _modelPath;
        private readonly bool _flipY;

        private PersonDetector? _detector;
        private PhysicsObject? _personBody;
        private readonly object _syncLock = new();
        
        // Queue for thread-safe polygon updates (detection runs on background thread)
        private readonly ConcurrentQueue<Vector2[]> _pendingPolygons = new();

        /// <summary>
        /// Creates a new PersonColliderBridge.
        /// </summary>
        /// <param name="worldWidth">Physics world width (for coordinate scaling).</param>
        /// <param name="worldHeight">Physics world height (for coordinate scaling).</param>
        /// <param name="modelPath">Path to the ONNX segmentation model.</param>
        /// <param name="flipY">Set true if SharpPhysics uses Y-up coordinates.</param>
        public PersonColliderBridge(
            float worldWidth,
            float worldHeight,
            string modelPath,
            bool flipY = false)
        {
            _worldWidth = worldWidth;
            _worldHeight = worldHeight;
            _modelPath = modelPath;
            _flipY = flipY;
        }

        /// <summary>
        /// Fired when an error occurs in detection.
        /// </summary>
        public event EventHandler<Exception>? OnError;

        /// <summary>
        /// Fired after the person body is updated in the physics world.
        /// </summary>
        public event EventHandler<PhysicsObject>? OnPersonBodyUpdated;

        /// <summary>
        /// The current person PhysicsObject (null if no person detected).
        /// </summary>
        public PhysicsObject? PersonBody
        {
            get { lock (_syncLock) return _personBody; }
        }

        /// <summary>
        /// Start detection with the specified camera.
        /// </summary>
        public void Start(int cameraIndex = 0, int width = 640, int height = 480)
        {
            try
            {
                // Configure ONNX options for FCN ResNet-50 model
                var options = new OnnxSegmentationOptions
                {
                    ModelPath = _modelPath,
                    InputTensorName = "input",
                    OutputTensorName = "out",  // FCN ResNet-50 uses "out" not "output"
                    InputWidth = 520,
                    InputHeight = 520,
                    Threshold = 0.5f
                };
                
                _detector = new PersonDetector(_modelPath, options);
                _detector.OnPersonDetected += HandlePersonDetected;
                _detector.OnError += (_, ex) => OnError?.Invoke(this, ex);
                _detector.Start(cameraIndex, width, height);
                
                Console.WriteLine($"PersonColliderBridge started with camera {cameraIndex} at {width}x{height}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to start PersonDetector: {ex.Message}");
                OnError?.Invoke(this, ex);
            }
        }

        /// <summary>
        /// Stop detection and remove person body from world.
        /// </summary>
        public void Stop()
        {
            _detector?.Stop();
            _detector?.Dispose();
            _detector = null;

            lock (_syncLock)
            {
                if (_personBody != null)
                {
                    PhysicsSystem.RemovalQueue.Enqueue(_personBody);
                    _personBody = null;
                }
            }
        }

        /// <summary>
        /// Call this from the main game loop to process any pending polygon updates.
        /// This ensures physics objects are created/updated on the main thread.
        /// </summary>
        public void ProcessPendingUpdates()
        {
            // Only process the most recent polygon (discard older ones)
            Vector2[]? latestPolygon = null;
            while (_pendingPolygons.TryDequeue(out var polygon))
            {
                latestPolygon = polygon;
            }

            if (latestPolygon != null && latestPolygon.Length >= 3)
            {
                UpdatePersonBody(latestPolygon);
            }
        }

        private void HandlePersonDetected(object? sender, PersonDetectedEventArgs e)
        {
            if (e.Polygons.Count == 0) return;

            // Use first polygon (largest/primary silhouette)
            var polygon = e.Polygons[0];
            
            if (polygon.Count < 3) return;

            // Convert to physics coordinates
            var physicsVerts = ConvertToPhysicsCoords(polygon);
            
            // Queue for main thread processing
            _pendingPolygons.Enqueue(physicsVerts);
        }

        private void UpdatePersonBody(Vector2[] physicsVerts)
        {
            // Calculate centroid for body position
            var centroid = CalculateCentroid(physicsVerts);

            // Convert to local coordinates (relative to centroid)
            var localVerts = physicsVerts
                .Select(v => new Vector2(v.X - centroid.X, v.Y - centroid.Y))
                .ToArray();

            lock (_syncLock)
            {
                // Remove existing body
                if (_personBody != null)
                {
                    PhysicsSystem.RemovalQueue.Enqueue(_personBody);
                }

                // Create new static body using the existing physics system
                var shader = new SFMLPolyShader();
                _personBody = PhysicsSystem.CreatePolygon(centroid, localVerts, shader, locked: true, canRotate: false);
            }

            OnPersonBodyUpdated?.Invoke(this, _personBody!);
        }

        private Vector2[] ConvertToPhysicsCoords(IReadOnlyList<Vector2> polygon)
        {
            return polygon.Select(p =>
            {
                float x = p.X * _worldWidth;
                float y = _flipY
                    ? _worldHeight - (p.Y * _worldHeight)
                    : p.Y * _worldHeight;
                return new Vector2(x, y);
            }).ToArray();
        }

        private static Vector2 CalculateCentroid(Vector2[] vertices)
        {
            float x = 0, y = 0;
            foreach (var v in vertices)
            {
                x += v.X;
                y += v.Y;
            }
            return new Vector2(x / vertices.Length, y / vertices.Length);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
