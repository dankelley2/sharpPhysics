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
        private readonly bool _flipX;

        private PersonDetector? _detector;
        private List<PhysicsObject> _personBodies = new();
        private readonly object _syncLock = new();
        
        // Queue for thread-safe polygon updates (detection runs on background thread)
        // Now holds lists of polygons to support multiple convex hulls per detection
        private readonly ConcurrentQueue<List<Vector2[]>> _pendingPolygonSets = new();

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
            bool flipX = true,
            bool flipY = false)
        {
            _worldWidth = worldWidth;
            _worldHeight = worldHeight;
            _modelPath = modelPath;
            _flipX = flipX;
            _flipY = flipY;
        }

        /// <summary>
        /// Fired when an error occurs in detection.
        /// </summary>
        public event EventHandler<Exception>? OnError;

        /// <summary>
        /// Fired after the person body is updated in the physics world.
        /// </summary>
        public event EventHandler<IReadOnlyList<PhysicsObject>>? OnPersonBodyUpdated;

        /// <summary>
        /// The current person PhysicsObjects (empty if no person detected).
        /// Multiple objects represent convex hull decomposition of concave silhouette.
        /// </summary>
        public IReadOnlyList<PhysicsObject> PersonBodies
        {
            get { lock (_syncLock) return _personBodies.ToList(); }
        }

        /// <summary>
        /// Start detection with the specified camera.
        /// </summary>
        public void Start(int cameraIndex = 0, int width = 640, int height = 480, int fps = 30)
        {
            try
            {
                // Configure ONNX options for MediaPipe Selfie Segmentation model
                var options = new OnnxSegmentationOptions
                {
                    ModelPath = _modelPath,
                    ModelType = ModelType.MediaPipeSelfie,
                    InputTensorName = "input_1:0",
                    OutputTensorName = "activation_10",
                    InputWidth = 256,
                    InputHeight = 256,
                    Threshold = 0.7f,
                    ExecutionProvider = ExecutionProvider.DirectML,  // GPU acceleration
                    GpuDeviceId = 0
                };
                
                _detector = new PersonDetector(_modelPath, options);
                _detector.OnPersonDetected += HandlePersonDetected;
                _detector.OnError += (_, ex) => OnError?.Invoke(this, ex);
                _detector.Start(cameraIndex, width, height, fps);
                
                Console.WriteLine($"PersonColliderBridge started with camera {cameraIndex} at {width}x{height} @ {fps}fps");
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
                // Remove all existing person bodies
                foreach (var body in _personBodies)
                {
                    PhysicsSystem.RemovalQueue.Enqueue(body);
                }
                _personBodies.Clear();
            }
        }

        /// <summary>
        /// Call this from the main game loop to process any pending polygon updates.
        /// This ensures physics objects are created/updated on the main thread.
        /// </summary>
        public void ProcessPendingUpdates()
        {
            // Only process the most recent polygon set (discard older ones)
            List<Vector2[]>? latestPolygonSet = null;
            while (_pendingPolygonSets.TryDequeue(out var polygonSet))
            {
                latestPolygonSet = polygonSet;
            }

            if (latestPolygonSet != null && latestPolygonSet.Count > 0)
            {
                UpdatePersonBodies(latestPolygonSet);
            }
        }

        private void HandlePersonDetected(object? sender, PersonDetectedEventArgs e)
        {
            if (e.Polygons.Count == 0) return;

            // Convert all polygons to physics coordinates
            var polygonSet = new List<Vector2[]>();
            
            foreach (var polygon in e.Polygons)
            {
                if (polygon.Count < 3) continue;
                
                var physicsVerts = ConvertToPhysicsCoords(polygon);
                polygonSet.Add(physicsVerts);
            }
            
            if (polygonSet.Count > 0)
            {
                // Queue for main thread processing
                _pendingPolygonSets.Enqueue(polygonSet);
            }
        }

        private void UpdatePersonBodies(List<Vector2[]> polygonSet)
        {
            lock (_syncLock)
            {
                // Remove all existing bodies
                foreach (var body in _personBodies)
                {
                    PhysicsSystem.RemovalQueue.Enqueue(body);
                }
                _personBodies.Clear();

                // Create new bodies for each convex hull
                foreach (var physicsVerts in polygonSet)
                {
                    if (physicsVerts.Length < 3) continue;
                    
                    // Calculate centroid for body position
                    var centroid = CalculateCentroid(physicsVerts);

                    // Convert to local coordinates (relative to centroid)
                    var localVerts = physicsVerts
                        .Select(v => new Vector2(v.X - centroid.X, v.Y - centroid.Y))
                        .ToArray();

                    // Create new static body using the existing physics system
                    var shader = new SFMLPolyShader();
                    var body = PhysicsSystem.CreatePolygon(centroid, localVerts, shader, locked: true, canRotate: false);
                    _personBodies.Add(body);
                }
            }

            if (_personBodies.Count > 0)
            {
                OnPersonBodyUpdated?.Invoke(this, _personBodies);
            }
        }

        /// <summary>
        /// Converts normalized coordinates (0-1) to physics world coordinates.
        /// </summary>
        private Vector2[] ConvertToPhysicsCoords(IReadOnlyList<Vector2> polygon)
        {
            return polygon.Select(p =>
            {
                float x = _flipX 
                    ? _worldWidth - (p.X * _worldWidth) 
                    : p.X * _worldWidth;
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
