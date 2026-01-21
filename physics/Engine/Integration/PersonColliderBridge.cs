#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using physics.Engine.Objects;
using physics.Engine.Shaders;
using ProjectorSegmentation.Vision.Models;
using ProjectorSegmentation.Vision.PoseDetection;
using ProjectorSegmentation.Vision.FrameSources;
using ProjectorSegmentation.Vision.Abstractions;

namespace physics.Engine.Integration
{
    /// <summary>
    /// Bridges ProjectorSegmentation pose detection with the SharpPhysics engine.
    /// Creates physics balls for hands and head that track detected keypoints.
    /// </summary>
    public sealed class PersonColliderBridge : IDisposable
    {
        private readonly PhysicsSystem _physicsSystem;
        private readonly float _worldWidth;
        private readonly float _worldHeight;
        private readonly string _modelPath;
        private readonly bool _flipY;
        private readonly bool _flipX;
        private readonly float _trackingSpeed;
        private readonly int _ballRadius;
        private readonly float _smoothingFactor;

        private IPoseDetector? _poseDetector;
        private IFrameSource? _camera;
        private Thread? _detectionThread;
        private CancellationTokenSource? _cts;
        private volatile bool _isRunning;

        // Physics balls for head and hands
        private PhysicsObject? _headBall;
        private PhysicsObject? _leftHandBall;
        private PhysicsObject? _rightHandBall;
        private readonly object _syncLock = new();

        // Queue for thread-safe keypoint updates (detection runs on background thread)
        private readonly ConcurrentQueue<PoseKeypoints> _pendingKeypoints = new();

        // Smoothed keypoint positions (for temporal smoothing)
        private Vector2 _smoothedHeadPos;
        private Vector2 _smoothedLeftHandPos;
        private Vector2 _smoothedRightHandPos;
        private bool _hasInitialPositions = false;

        // Full skeleton keypoints (all 17 COCO keypoints)
        // Raw positions from detection
        private Vector2[] _rawKeypoints = new Vector2[17];
        // Smoothed positions for rendering
        private Vector2[] _smoothedKeypoints = new Vector2[17];
        private float[] _keypointConfidences = new float[17];
        private bool _hasFullSkeleton = false;

        // Skeleton transform parameters (adjustable from game engine)
        private Vector2 _skeletonOffset = Vector2.Zero;
        private float _skeletonScale = 0.5f;
        private Vector2 _skeletonOrigin = new Vector2(0.5f, 1f); // Normalized origin point for scaling

        // Keypoint indices for Yolo COCO format
        private const int NOSE_INDEX = 0;
        private const int LEFT_WRIST_INDEX = 9;
        private const int RIGHT_WRIST_INDEX = 10;

        // COCO skeleton connections (17 keypoints)
        private static readonly (int, int)[] SkeletonConnections = new[]
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
        /// Stores the relevant keypoints for physics tracking.
        /// </summary>
        private record PoseKeypoints(
            Vector2 HeadPos, float HeadConf,
            Vector2 LeftHandPos, float LeftHandConf,
            Vector2 RightHandPos, float RightHandConf
        );

        /// <summary>
        /// Creates a new PersonColliderBridge for pose-based physics interaction.
        /// </summary>
        /// <param name="physicsSystem">The physics system to create tracking balls in.</param>
        /// <param name="worldWidth">Physics world width (for coordinate scaling).</param>
        /// <param name="worldHeight">Physics world height (for coordinate scaling).</param>
        /// <param name="modelPath">Path to the YOLO Pose ONNX model.</param>
        /// <param name="flipX">Flip X coordinates (mirror mode).</param>
        /// <param name="flipY">Flip Y coordinates.</param>
        /// <param name="trackingSpeed">How fast balls move toward target positions (higher = faster).</param>
        /// <param name="ballRadius">Radius of the tracking balls (default 20).</param>
        /// <param name="smoothingFactor">Temporal smoothing factor (0 = no smoothing, 0.8 = very smooth). Default 0.5.</param>
        public PersonColliderBridge(
            PhysicsSystem physicsSystem,
            float worldWidth,
            float worldHeight,
            string modelPath,
            bool flipX = true,  // Mirror by default for natural interaction
            bool flipY = false,
            float trackingSpeed = 15f,
            int ballRadius = 20,
            float smoothingFactor = 0.75f)
        {
            _physicsSystem = physicsSystem;
            _worldWidth = worldWidth;
            _worldHeight = worldHeight;
            _modelPath = modelPath;
            _flipX = flipX;
            _flipY = flipY;
            _trackingSpeed = trackingSpeed;
            _ballRadius = ballRadius;
            _smoothingFactor = Math.Clamp(smoothingFactor, 0f, 0.95f);
        }

        /// <summary>
        /// Gets or sets the skeleton offset in world coordinates.
        /// Use this to move the entire skeleton around the screen.
        /// </summary>
        public Vector2 SkeletonOffset
        {
            get => _skeletonOffset;
            set => _skeletonOffset = value;
        }

        /// <summary>
        /// Gets or sets the skeleton scale factor.
        /// 1.0 = full screen, 0.5 = half size, 2.0 = double size.
        /// </summary>
        public float SkeletonScale
        {
            get => _skeletonScale;
            set => _skeletonScale = Math.Max(0.1f, value);
        }

        /// <summary>
        /// Gets or sets the origin point for scaling (in normalized 0-1 coordinates).
        /// Default is (0.5, 0.5) = center of screen.
        /// </summary>
        public Vector2 SkeletonOrigin
        {
            get => _skeletonOrigin;
            set => _skeletonOrigin = value;
        }

        /// <summary>
        /// Fired when an error occurs in detection.
        /// </summary>
        public event EventHandler<Exception>? OnError;

        /// <summary>
        /// Fired after the tracking balls are updated.
        /// </summary>
        public event EventHandler<IReadOnlyList<PhysicsObject>>? OnPersonBodyUpdated;

        /// <summary>
        /// The current tracking balls (head, left hand, right hand).
        /// </summary>
        public IReadOnlyList<PhysicsObject> TrackingBalls
        {
            get
            {
                lock (_syncLock)
                {
                    var balls = new List<PhysicsObject>();
                    if (_headBall != null) balls.Add(_headBall);
                    if (_leftHandBall != null) balls.Add(_leftHandBall);
                    if (_rightHandBall != null) balls.Add(_rightHandBall);
                    return balls;
                }
            }
        }

        /// <summary>
        /// Start pose detection with the specified camera.
        /// </summary>
        public void Start(string url, int width = 640, int height = 480, int fps = 30)
        {
            try
            {
                Console.WriteLine($"[PersonBridge] Loading model from: {System.IO.Path.GetFullPath(_modelPath)}");

                if (!System.IO.File.Exists(_modelPath))
                {
                    throw new System.IO.FileNotFoundException($"Model file not found: {_modelPath}");
                }

                _poseDetector = new YoloV26PoseDetector(_modelPath, useGpu: true);
                Console.WriteLine("[PersonBridge] Yolo model loaded successfully");

                _camera = new MjpegCameraFrameSource(url, 5, true);
                Console.WriteLine($"[PersonBridge] Stream {url} opened at {width}x{height} @ {fps}fps");

                // Create the tracking balls
                CreateTrackingBalls();

                _cts = new CancellationTokenSource();
                _isRunning = true;

                _detectionThread = new Thread(() => DetectionLoop(_cts.Token))
                {
                    Name = "PersonColliderBridge-Detection",
                    IsBackground = true
                };
                _detectionThread.Start();

                Console.WriteLine($"[PersonBridge] Detection thread started");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PersonBridge] Failed to start: {ex.Message}");
                Console.WriteLine($"[PersonBridge] Stack trace: {ex.StackTrace}");
                OnError?.Invoke(this, ex);
            }
        }

        /// <summary>
        /// Start pose detection with the specified camera.
        /// </summary>
        public void Start(int cameraIndex = 0, int width = 640, int height = 480, int fps = 30)
        {
            try
            {
                Console.WriteLine($"[PersonBridge] Loading model from: {System.IO.Path.GetFullPath(_modelPath)}");

                if (!System.IO.File.Exists(_modelPath))
                {
                    throw new System.IO.FileNotFoundException($"Model file not found: {_modelPath}");
                }

                _poseDetector = new YoloV26PoseDetector(_modelPath, useGpu: true);
                Console.WriteLine("[PersonBridge] Yolo model loaded successfully");

                _camera = new OpenCvCameraFrameSource(cameraIndex, width, height, fps);
                Console.WriteLine($"[PersonBridge] Camera {cameraIndex} opened at {width}x{height} @ {fps}fps");

                // Create the tracking balls
                CreateTrackingBalls();

                _cts = new CancellationTokenSource();
                _isRunning = true;

                _detectionThread = new Thread(() => DetectionLoop(_cts.Token))
                {
                    Name = "PersonColliderBridge-Detection",
                    IsBackground = true
                };
                _detectionThread.Start();

                Console.WriteLine($"[PersonBridge] Detection thread started");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PersonBridge] Failed to start: {ex.Message}");
                Console.WriteLine($"[PersonBridge] Stack trace: {ex.StackTrace}");
                OnError?.Invoke(this, ex);
            }
        }

        /// <summary>
        /// Creates the initial tracking balls for head and hands.
        /// </summary>
        private void CreateTrackingBalls()
        {
            lock (_syncLock)
            {
                // Create balls at center of screen initially
                var centerPos = new Vector2(_worldWidth / 2, _worldHeight / 2);

                // Head ball (slightly above center) - locked so gravity doesn't affect it
                var headShader = new SFMLPolyShader();
                _headBall = _physicsSystem.CreateStaticCircle(
                    centerPos - new Vector2(0, 100),
                    _ballRadius,
                    0.8f,
                    locked: true,  // Locked so gravity doesn't affect it
                    headShader
                );

                // Left hand ball
                var leftHandShader = new SFMLBallShader();
                _leftHandBall = _physicsSystem.CreateStaticCircle(
                    centerPos - new Vector2(100, 0),
                    _ballRadius,
                    0.8f,
                    locked: true,
                    leftHandShader
                );

                // Right hand ball
                var rightHandShader = new SFMLBallShader();
                _rightHandBall = _physicsSystem.CreateStaticCircle(
                    centerPos + new Vector2(100, 0),
                    _ballRadius,
                    0.8f,
                    locked: true,
                    rightHandShader
                );
            }
        }

        /// <summary>
        /// Background thread detection loop.
        /// </summary>
        private void DetectionLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _isRunning)
            {
                try
                {
                    if (_camera == null || _poseDetector == null)
                        break;

                    if (!_camera.TryGetFrame(out var frame, out var timestamp))
                    {
                        Thread.Sleep(5);
                        continue;
                    }

                    try
                    {
                        var poseResult = _poseDetector.Detect(frame, timestamp);

                        if (poseResult.PersonDetected && poseResult.Keypoints.Count >= 17)
                        {
                            var keypoints = ExtractRelevantKeypoints(poseResult.Keypoints);
                            if (keypoints != null)
                            {
                                _pendingKeypoints.Enqueue(keypoints);
                            }
                        }
                    }
                    finally
                    {
                        frame.Dispose();
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Console.WriteLine($"[PersonBridge] Detection error: {ex.Message}");
                    OnError?.Invoke(this, ex);
                    Thread.Sleep(100);
                }
            }
        }

        /// <summary>
        /// Extracts head and hand keypoints from the pose result.
        /// Also stores all keypoints for full skeleton rendering.
        /// </summary>
        private PoseKeypoints? ExtractRelevantKeypoints(IReadOnlyList<Keypoint> keypoints)
        {
            if (keypoints.Count < 17)
                return null;

            var nose = keypoints[NOSE_INDEX];
            var leftWrist = keypoints[LEFT_WRIST_INDEX];
            var rightWrist = keypoints[RIGHT_WRIST_INDEX];

            // Convert normalized coordinates (0-1) to physics world coordinates
            var headPos = ConvertToPhysicsCoords(nose.X, nose.Y);
            var leftHandPos = ConvertToPhysicsCoords(leftWrist.X, leftWrist.Y);
            var rightHandPos = ConvertToPhysicsCoords(rightWrist.X, rightWrist.Y);

            // Store all 17 RAW keypoints for smoothing later
            lock (_syncLock)
            {
                for (int i = 0; i < 17 && i < keypoints.Count; i++)
                {
                    var kp = keypoints[i];
                    _rawKeypoints[i] = ConvertToPhysicsCoords(kp.X, kp.Y);
                    _keypointConfidences[i] = kp.Confidence;
                }
                _hasFullSkeleton = true;
            }

            return new PoseKeypoints(
                headPos, nose.Confidence,
                leftHandPos, leftWrist.Confidence,
                rightHandPos, rightWrist.Confidence
            );
        }

        /// <summary>
        /// Converts normalized coordinates (0-1) to physics world coordinates.
        /// Applies skeleton transform (scale and offset).
        /// </summary>
        private Vector2 ConvertToPhysicsCoords(float normX, float normY)
        {
            // Flip coordinates if needed
            float x = _flipX ? (1 - normX) : normX;
            float y = _flipY ? (1 - normY) : normY;

            // Apply scale around the origin point
            float scaledX = _skeletonOrigin.X + (x - _skeletonOrigin.X) * _skeletonScale;
            float scaledY = _skeletonOrigin.Y + (y - _skeletonOrigin.Y) * _skeletonScale;

            // Convert to world coordinates and apply offset
            return new Vector2(
                scaledX * _worldWidth + _skeletonOffset.X,
                scaledY * _worldHeight + _skeletonOffset.Y
            );
        }

        /// <summary>
        /// Lerp (linear interpolation) between two vectors.
        /// </summary>
        private static Vector2 Lerp(Vector2 from, Vector2 to, float t)
        {
            return from + (to - from) * t;
        }

        /// <summary>
        /// Applies temporal smoothing using lerp toward target position.
        /// Higher smoothing factor = slower movement toward target.
        /// </summary>
        private Vector2 SmoothPosition(Vector2 target, Vector2 current, bool hasInitial)
        {
            if (!hasInitial || _smoothingFactor <= 0)
            {
                return target;
            }

            // Lerp factor: lower smoothingFactor = faster response
            // smoothingFactor of 0.75 means we move 25% of the way each frame
            float lerpSpeed = 1f - _smoothingFactor;

            return Lerp(current, target, lerpSpeed);
        }

        /// <summary>
        /// Stop detection and remove tracking balls from world.
        /// </summary>
        public void Stop()
        {
            _isRunning = false;
            _cts?.Cancel();

            _detectionThread?.Join(TimeSpan.FromSeconds(2));
            _detectionThread = null;

            _cts?.Dispose();
            _cts = null;

            _camera?.Dispose();
            _camera = null;

            _poseDetector?.Dispose();
            _poseDetector = null;

            lock (_syncLock)
            {
                if (_headBall != null)
                {
                    _physicsSystem.RemovalQueue.Enqueue(_headBall);
                    _headBall = null;
                }
                if (_leftHandBall != null)
                {
                    _physicsSystem.RemovalQueue.Enqueue(_leftHandBall);
                    _leftHandBall = null;
                }
                if (_rightHandBall != null)
                {
                    _physicsSystem.RemovalQueue.Enqueue(_rightHandBall);
                    _rightHandBall = null;
                }
            }
        }

        /// <summary>
        /// Call this from the main game loop to process any pending keypoint updates.
        /// This ensures physics objects are updated on the main thread.
        /// </summary>
        public void ProcessPendingUpdates()
        {
            // Only process the most recent keypoints (discard older ones)
            PoseKeypoints? latestKeypoints = null;
            int discardCount = 0;

            while (_pendingKeypoints.TryDequeue(out var keypoints))
            {
                if (latestKeypoints != null) discardCount++;
                latestKeypoints = keypoints;
            }

            if (latestKeypoints != null)
            {
                UpdateTrackingBalls(latestKeypoints);
            }
        }

        /// <summary>
        /// Updates the tracking balls to move toward the detected keypoint positions.
        /// Applies smoothing and transforms before moving balls.
        /// </summary>
        private void UpdateTrackingBalls(PoseKeypoints keypoints)
        {
            lock (_syncLock)
            {
                // Initialize positions on first detection
                if (!_hasInitialPositions)
                {
                    _smoothedHeadPos = keypoints.HeadPos;
                    _smoothedLeftHandPos = keypoints.LeftHandPos;
                    _smoothedRightHandPos = keypoints.RightHandPos;

                    // Initialize all skeleton keypoints
                    Array.Copy(_rawKeypoints, _smoothedKeypoints, 17);
                    _hasInitialPositions = true;
                }

                // Apply lerp smoothing to head and hands
                _smoothedHeadPos = SmoothPosition(keypoints.HeadPos, _smoothedHeadPos, true);
                _smoothedLeftHandPos = SmoothPosition(keypoints.LeftHandPos, _smoothedLeftHandPos, true);
                _smoothedRightHandPos = SmoothPosition(keypoints.RightHandPos, _smoothedRightHandPos, true);

                // Apply lerp smoothing to all skeleton keypoints
                for (int i = 0; i < 17; i++)
                {
                    _smoothedKeypoints[i] = SmoothPosition(_rawKeypoints[i], _smoothedKeypoints[i], true);
                }

                // Update head ball
                if (_headBall != null && keypoints.HeadConf > 0.5f)
                {
                    MoveBallToPosition(_headBall, _smoothedHeadPos);
                }

                // Update left hand ball
                if (_leftHandBall != null && keypoints.LeftHandConf > 0.5f)
                {
                    MoveBallToPosition(_leftHandBall, _smoothedLeftHandPos);
                }

                // Update right hand ball
                if (_rightHandBall != null && keypoints.RightHandConf > 0.5f)
                {
                    MoveBallToPosition(_rightHandBall, _smoothedRightHandPos);
                }

                // Store smoothed keypoints for skeleton rendering
                _latestKeypoints = new PoseKeypoints(
                    _smoothedHeadPos, keypoints.HeadConf,
                    _smoothedLeftHandPos, keypoints.LeftHandConf,
                    _smoothedRightHandPos, keypoints.RightHandConf
                );
            }

            OnPersonBodyUpdated?.Invoke(this, TrackingBalls);
        }

        /// <summary>
        /// Directly moves a ball to the target position (already smoothed).
        /// Temporarily unlocks to allow movement, then re-locks.
        /// </summary>
        private void MoveBallToPosition(PhysicsObject ball, Vector2 targetPos)
        {
            var currentPos = ball.Center;
            var delta = targetPos - currentPos;

            // Temporarily unlock to move
            ball.Locked = false;
            ball.Velocity = delta * 10;
            ball.Move(delta);
            ball.Locked = true;

            // Zero out any velocity
            //ball.Velocity = Vector2.Zero;
        }

        // Store latest keypoints for external access (e.g., skeleton rendering)
        private PoseKeypoints? _latestKeypoints;

        /// <summary>
        /// Gets the latest detected keypoints for rendering (head and hands only).
        /// Returns null if no keypoints have been detected yet.
        /// </summary>
        public (Vector2 Head, Vector2 LeftHand, Vector2 RightHand, float HeadConf, float LeftConf, float RightConf)? GetLatestKeypoints()
        {
            var kp = _latestKeypoints;
            if (kp == null) return null;
            return (kp.HeadPos, kp.LeftHandPos, kp.RightHandPos, kp.HeadConf, kp.LeftHandConf, kp.RightHandConf);
        }

        /// <summary>
        /// Gets the full skeleton data for rendering (all 17 COCO keypoints).
        /// Returns null if no skeleton has been detected yet.
        /// </summary>
        public (Vector2[] Keypoints, float[] Confidences, (int, int)[] Connections)? GetFullSkeleton()
        {
            lock (_syncLock)
            {
                if (!_hasFullSkeleton) return null;

                // Return copies to avoid threading issues
                var keypoints = new Vector2[17];
                var confidences = new float[17];
                Array.Copy(_smoothedKeypoints, keypoints, 17);
                Array.Copy(_keypointConfidences, confidences, 17);

                return (keypoints, confidences, SkeletonConnections);
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
