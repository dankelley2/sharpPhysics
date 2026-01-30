#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using physics.Engine;
using physics.Engine.Objects;
using physics.Engine.Shaders;
using PoseIntegrator.Vision.Models;
using PoseIntegrator.Vision.PoseDetection;
using PoseIntegrator.Vision.FrameSources;
using PoseIntegrator.Vision.Abstractions;

namespace SharpPhysics.Demo.Integration
{

    /// <summary>
    /// Bridges PoseIntegrator pose detection with the SharpPhysics engine.
    /// Creates physics balls for hands and head that track detected keypoints.
    /// Supports multiple simultaneous people detection.
    /// </summary>
    public sealed class PersonColliderBridge : IDisposable
    {
        private readonly PhysicsSystem _physicsSystem;
        private readonly float _worldWidth;
        private readonly float _worldHeight;
        private readonly string _modelPath;
        private readonly bool _flipY;
        private readonly bool _flipX;
        private readonly int _ballRadius;
        private readonly float _smoothingFactor;
        private readonly int _maxPeople;

        private IPoseDetector? _poseDetector;
        private IFrameSource? _camera;
        private Thread? _detectionThread;
        private CancellationTokenSource? _cts;
        private volatile bool _isRunning;

        // Multi-person tracking
        private readonly Dictionary<int, TrackedPerson> _trackedPeople = new();
        private readonly object _syncLock = new();

        // Queue for thread-safe detection updates (detection runs on background thread)
        private readonly ConcurrentQueue<PoseDetectionResult> _pendingResults = new();

        // Timeout for removing stale tracked people (ms)
        private const long TRACKING_TIMEOUT_MS = 500;

        // Skeleton transform parameters (adjustable from game engine)
        private Vector2 _skeletonOffset = Vector2.Zero;
        private float _skeletonScale = 0.5f;
        private Vector2 _skeletonOrigin = new Vector2(0.5f, 1f);

        // Keypoint indices for Yolo COCO format
        private const int NOSE_INDEX = 0;
        private const int LEFT_WRIST_INDEX = 9;
        private const int RIGHT_WRIST_INDEX = 10;

        // COCO skeleton connections (17 keypoints) - made public for external rendering
        public static readonly (int, int)[] SkeletonConnections = new[]
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
        /// Creates a new PersonColliderBridge for pose-based physics interaction.
        /// </summary>
        /// <param name="physicsSystem">The physics system to create tracking balls in.</param>
        /// <param name="worldWidth">Physics world width (for coordinate scaling).</param>
        /// <param name="worldHeight">Physics world height (for coordinate scaling).</param>
        /// <param name="modelPath">Path to the YOLO Pose ONNX model.</param>
        /// <param name="flipX">Flip X coordinates (mirror mode).</param>
        /// <param name="flipY">Flip Y coordinates.</param>
        /// <param name="ballRadius">Radius of the tracking balls (default 20).</param>
        /// <param name="smoothingFactor">Temporal smoothing factor (0 = no smoothing, 0.8 = very smooth). Default 0.75.</param>
        /// <param name="maxPeople">Maximum number of people to track simultaneously (default 5).</param>
        public PersonColliderBridge(
            PhysicsSystem physicsSystem,
            float worldWidth,
            float worldHeight,
            string modelPath,
            bool flipX = true,  // Mirror by default for natural interaction
            bool flipY = false,
            int ballRadius = 20,
            float smoothingFactor = 0.75f,
            int maxPeople = 5)
        {
            _physicsSystem = physicsSystem;
            _worldWidth = worldWidth;
            _worldHeight = worldHeight;
            _modelPath = modelPath;
            _flipX = flipX;
            _flipY = flipY;
            _ballRadius = ballRadius;
            _smoothingFactor = Math.Clamp(smoothingFactor, 0f, 0.95f);
            _maxPeople = Math.Max(1, maxPeople);
        }

        /// <summary>
        /// Gets or sets the skeleton offset in world coordinates.
        /// </summary>
        public Vector2 SkeletonOffset
        {
            get => _skeletonOffset;
            set => _skeletonOffset = value;
        }

        /// <summary>
        /// Gets or sets the skeleton scale factor.
        /// </summary>
        public float SkeletonScale
        {
            get => _skeletonScale;
            set => _skeletonScale = Math.Max(0.1f, value);
        }

        /// <summary>
        /// Gets or sets the origin point for scaling (in normalized 0-1 coordinates).
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
        /// Fired after tracking balls are updated for any person.
        /// </summary>
        public event EventHandler<IReadOnlyList<PhysicsObject>>? OnPersonBodyUpdated;

        /// <summary>
        /// Number of currently tracked people.
        /// </summary>
        public int TrackedPersonCount
        {
            get
            {
                lock (_syncLock)
                {
                    return _trackedPeople.Count;
                }
            }
        }

        /// <summary>
        /// All current tracking balls across all tracked people.
        /// </summary>
        public IReadOnlyList<PhysicsObject> TrackingBalls
        {
            get
            {
                lock (_syncLock)
                {
                    return _trackedPeople.Values
                        .SelectMany(p => p.GetTrackingBalls())
                        .ToList();
                }
            }
        }

        /// <summary>
        /// Gets all currently tracked people.
        /// </summary>
        public IReadOnlyList<TrackedPerson> GetTrackedPeople()
        {
            lock (_syncLock)
            {
                return _trackedPeople.Values.ToList();
            }
        }

        /// <summary>
        /// Start pose detection with the specified MJPEG stream URL.
        /// </summary>
        public void Start(string url, int fps = 30)
        {
            try
            {
                Console.WriteLine($"[PersonBridge] Loading model from: {System.IO.Path.GetFullPath(_modelPath)}");

                if (!System.IO.File.Exists(_modelPath))
                {
                    throw new System.IO.FileNotFoundException($"Model file not found: {_modelPath}");
                }

                _poseDetector = new YoloV26PoseDetector(_modelPath, useGpu: true, maxPeople: _maxPeople);
                Console.WriteLine("[PersonBridge] Yolo model loaded successfully");

                _camera = new MjpegCameraFrameSource(url, 5, true);
                Console.WriteLine($"[PersonBridge] Stream {url} opened");
                Console.WriteLine($"[PersonBridge] Multi-person tracking enabled (max {_maxPeople} people)");

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
        /// Start pose detection with the specified camera index.
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

                _poseDetector = new YoloV26PoseDetector(_modelPath, useGpu: true, maxPeople: _maxPeople);
                Console.WriteLine("[PersonBridge] Yolo model loaded successfully");

                _camera = new OpenCvCameraFrameSource(cameraIndex, width, height, fps);
                Console.WriteLine($"[PersonBridge] Camera {cameraIndex} opened at {width}x{height} @ {fps}fps");
                Console.WriteLine($"[PersonBridge] Multi-person tracking enabled (max {_maxPeople} people)");

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
        /// Creates tracking balls for a newly detected person.
        /// </summary>
        private TrackedPerson CreateTrackedPerson(int personId, Vector2 initialPos)
        {
            var person = new TrackedPerson(personId);

            // Head ball
            var headShader = new SFMLPolyShader();
            person.HeadBall = _physicsSystem.CreateStaticCircle(
                initialPos - new Vector2(0, 50),
                _ballRadius,
                0.8f,
                locked: true,
                headShader
            );

            // Left hand ball
            var leftHandShader = new SFMLBallShader();
            person.LeftHandBall = _physicsSystem.CreateStaticCircle(
                initialPos - new Vector2(50, 0),
                _ballRadius,
                0.8f,
                locked: true,
                leftHandShader
            );

            // Right hand ball
            var rightHandShader = new SFMLBallShader();
            person.RightHandBall = _physicsSystem.CreateStaticCircle(
                initialPos + new Vector2(50, 0),
                _ballRadius,
                0.8f,
                locked: true,
                rightHandShader
            );

            return person;
        }

        /// <summary>
        /// Removes a tracked person and their physics balls.
        /// </summary>
        private void RemoveTrackedPerson(TrackedPerson person)
        {
            if (person.HeadBall != null)
            {
                _physicsSystem.RemovalQueue.Enqueue(person.HeadBall);
            }
            if (person.LeftHandBall != null)
            {
                _physicsSystem.RemovalQueue.Enqueue(person.LeftHandBall);
            }
            if (person.RightHandBall != null)
            {
                _physicsSystem.RemovalQueue.Enqueue(person.RightHandBall);
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
                    var camera = _camera;
                    var poseDetector = _poseDetector;

                    if (camera == null || poseDetector == null || !_isRunning)
                        break;

                    if (!camera.TryGetFrame(out var frame, out var timestamp))
                    {
                        Thread.Sleep(5);
                        continue;
                    }

                    try
                    {
                        if (!_isRunning || ct.IsCancellationRequested)
                        {
                            frame.Dispose();
                            break;
                        }

                        var poseResult = poseDetector.Detect(frame, timestamp);

                        if (poseResult.PersonDetected && _isRunning)
                        {
                            _pendingResults.Enqueue(poseResult);
                        }
                    }
                    finally
                    {
                        frame.Dispose();
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (_isRunning)
                    {
                        Console.WriteLine($"[PersonBridge] Detection error: {ex.Message}");
                        OnError?.Invoke(this, ex);
                    }
                    Thread.Sleep(100);
                }
            }

            Console.WriteLine("[PersonBridge] Detection loop exited");
        }

        /// <summary>
        /// Converts normalized coordinates (0-1) to physics world coordinates.
        /// </summary>
        private Vector2 ConvertToPhysicsCoords(float normX, float normY)
        {
            float x = _flipX ? (1 - normX) : normX;
            float y = _flipY ? (1 - normY) : normY;

            float scaledX = _skeletonOrigin.X + (x - _skeletonOrigin.X) * _skeletonScale;
            float scaledY = _skeletonOrigin.Y + (y - _skeletonOrigin.Y) * _skeletonScale;

            return new Vector2(
                scaledX * _worldWidth + _skeletonOffset.X,
                scaledY * _worldHeight + _skeletonOffset.Y
            );
        }

        private static Vector2 Lerp(Vector2 from, Vector2 to, float t)
        {
            return from + (to - from) * t;
        }

        private Vector2 SmoothPosition(Vector2 target, Vector2 current, bool hasInitial)
        {
            if (!hasInitial || _smoothingFactor <= 0)
            {
                return target;
            }

            float lerpSpeed = 1f - _smoothingFactor;
            return Lerp(current, target, lerpSpeed);
        }

        /// <summary>
        /// Stop detection and remove all tracking balls from world.
        /// </summary>
        public void Stop()
        {
            if (!_isRunning && _detectionThread == null)
                return;

            Console.WriteLine("[PersonBridge] Stopping...");

            _isRunning = false;

            try
            {
                _cts?.Cancel();
            }
            catch (ObjectDisposedException) { }

            var thread = _detectionThread;
            if (thread != null && thread.IsAlive)
            {
                if (!thread.Join(TimeSpan.FromSeconds(3)))
                {
                    Console.WriteLine("[PersonBridge] Warning: Detection thread did not stop in time");
                }
            }
            _detectionThread = null;

            try
            {
                _cts?.Dispose();
            }
            catch (ObjectDisposedException) { }
            _cts = null;

            try
            {
                _camera?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PersonBridge] Error disposing camera: {ex.Message}");
            }
            _camera = null;

            try
            {
                _poseDetector?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PersonBridge] Error disposing detector: {ex.Message}");
            }
            _poseDetector = null;

            // Clear all tracked people
            lock (_syncLock)
            {
                foreach (var person in _trackedPeople.Values)
                {
                    RemoveTrackedPerson(person);
                }
                _trackedPeople.Clear();
            }

            while (_pendingResults.TryDequeue(out _)) { }

            Console.WriteLine("[PersonBridge] Stopped");
        }

        /// <summary>
        /// Call this from the main game loop to process any pending detection updates.
        /// </summary>
        public void ProcessPendingUpdates()
        {
            if (!_isRunning) return;

            // Only process the most recent result
            PoseDetectionResult? latestResult = null;
            while (_pendingResults.TryDequeue(out var result))
            {
                latestResult = result;
            }

            if (latestResult != null && _isRunning)
            {
                UpdateAllTrackedPeople(latestResult);
            }
        }

        /// <summary>
        /// Updates all tracked people based on the detection result.
        /// Handles creating new tracked people and removing stale ones.
        /// </summary>
        private void UpdateAllTrackedPeople(PoseDetectionResult result)
        {
            lock (_syncLock)
            {
                var currentTimestamp = result.TimestampMs;
                var detectedPersonIds = new HashSet<int>();

                // Update or create tracked people for each detected person
                foreach (var detectedPerson in result.People)
                {
                    // Match detected person to existing tracked person by spatial proximity
                    int matchedId = FindBestMatch(detectedPerson);

                    if (matchedId == -1)
                    {
                        // New person detected - create tracking
                        matchedId = GetNextAvailablePersonId();
                        if (_trackedPeople.Count >= _maxPeople)
                        {
                            // Skip if at max capacity
                            continue;
                        }

                        var initialPos = new Vector2(_worldWidth / 2, _worldHeight / 2);
                        _trackedPeople[matchedId] = CreateTrackedPerson(matchedId, initialPos);
                        Console.WriteLine($"[PersonBridge] New person detected, ID: {matchedId}");
                    }

                    detectedPersonIds.Add(matchedId);
                    UpdateTrackedPerson(_trackedPeople[matchedId], detectedPerson, currentTimestamp);
                }

                // Remove stale tracked people (not seen recently)
                var staleIds = _trackedPeople.Keys
                    .Where(id => !detectedPersonIds.Contains(id) &&
                                 (currentTimestamp - _trackedPeople[id].LastSeenTimestamp) > TRACKING_TIMEOUT_MS)
                    .ToList();

                foreach (var staleId in staleIds)
                {
                    Console.WriteLine($"[PersonBridge] Removing stale person, ID: {staleId}");
                    RemoveTrackedPerson(_trackedPeople[staleId]);
                    _trackedPeople.Remove(staleId);
                }
            }

            OnPersonBodyUpdated?.Invoke(this, TrackingBalls);
        }

        /// <summary>
        /// Finds the best matching tracked person for a detected person based on spatial proximity.
        /// Returns -1 if no good match is found.
        /// </summary>
        private int FindBestMatch(DetectedPerson detectedPerson)
        {
            // Get the detected person's center (use bbox center or nose position)

            var nose = detectedPerson.Keypoints[NOSE_INDEX];

            var headPos = ConvertToPhysicsCoords(nose.X, nose.Y);

            int bestMatchId = -1;
            float bestDistance = float.MaxValue;
            const float MAX_MATCH_DISTANCE = 200f; // Max pixels to consider a match

            foreach (var (id, tracked) in _trackedPeople)
            {
                if (!tracked.HasInitialPositions)
                    continue;

                // Use head position as the tracking reference
                var trackedCenter = tracked.SmoothedHeadPos;
                var distance = Vector2.Distance(headPos, trackedCenter);

                if (distance < bestDistance && distance < MAX_MATCH_DISTANCE)
                {
                    bestDistance = distance;
                    bestMatchId = id;
                }
            }

            return bestMatchId;
        }

        private int _nextPersonId = 0;
        private int GetNextAvailablePersonId()
        {
            return _nextPersonId++;
        }

        /// <summary>
        /// Updates a single tracked person with new detection data.
        /// </summary>
        private void UpdateTrackedPerson(TrackedPerson tracked, DetectedPerson detected, long timestamp)
        {
            tracked.LastSeenTimestamp = timestamp;
            tracked.Confidence = detected.Confidence;

            var keypoints = detected.Keypoints;
            if (keypoints.Count < 17)
                return;

            var nose = keypoints[NOSE_INDEX];
            var leftWrist = keypoints[LEFT_WRIST_INDEX];
            var rightWrist = keypoints[RIGHT_WRIST_INDEX];

            var headPos = ConvertToPhysicsCoords(nose.X, nose.Y);
            var leftHandPos = ConvertToPhysicsCoords(leftWrist.X, leftWrist.Y);
            var rightHandPos = ConvertToPhysicsCoords(rightWrist.X, rightWrist.Y);

            // Store raw keypoints
            for (int i = 0; i < 17 && i < keypoints.Count; i++)
            {
                var kp = keypoints[i];
                tracked.RawKeypoints[i] = ConvertToPhysicsCoords(kp.X, kp.Y);
                tracked.KeypointConfidences[i] = kp.Confidence;
            }

            // Initialize or smooth positions
            if (!tracked.HasInitialPositions)
            {
                tracked.SmoothedHeadPos = headPos;
                tracked.SmoothedLeftHandPos = leftHandPos;
                tracked.SmoothedRightHandPos = rightHandPos;
                Array.Copy(tracked.RawKeypoints, tracked.SmoothedKeypoints, 17);
                tracked.HasInitialPositions = true;
            }
            else
            {
                tracked.SmoothedHeadPos = SmoothPosition(headPos, tracked.SmoothedHeadPos, true);
                tracked.SmoothedLeftHandPos = SmoothPosition(leftHandPos, tracked.SmoothedLeftHandPos, true);
                tracked.SmoothedRightHandPos = SmoothPosition(rightHandPos, tracked.SmoothedRightHandPos, true);

                for (int i = 0; i < 17; i++)
                {
                    tracked.SmoothedKeypoints[i] = SmoothPosition(tracked.RawKeypoints[i], tracked.SmoothedKeypoints[i], true);
                }
            }

            // Update physics balls
            if (tracked.HeadBall != null && nose.Confidence > 0.5f)
            {
                MoveBallToPosition(tracked.HeadBall, tracked.SmoothedHeadPos);
            }

            if (tracked.LeftHandBall != null && leftWrist.Confidence > 0.5f)
            {
                MoveBallToPosition(tracked.LeftHandBall, tracked.SmoothedLeftHandPos);
            }

            if (tracked.RightHandBall != null && rightWrist.Confidence > 0.5f)
            {
                MoveBallToPosition(tracked.RightHandBall, tracked.SmoothedRightHandPos);
            }
        }

        private static void MoveBallToPosition(PhysicsObject ball, Vector2 targetPos)
        {
            var currentPos = ball.Center;
            var delta = targetPos - currentPos;

            ball.Locked = false;
            ball.Velocity = delta * 10;
            ball.Move(delta);
            ball.Locked = true;
        }

        /// <summary>
        /// Gets the latest keypoints for the first tracked person (backward compatibility).
        /// </summary>
        public (Vector2 Head, Vector2 LeftHand, Vector2 RightHand, float HeadConf, float LeftConf, float RightConf)? GetLatestKeypoints()
        {
            lock (_syncLock)
            {
                var firstPerson = _trackedPeople.Values.FirstOrDefault();
                if (firstPerson == null || !firstPerson.HasInitialPositions)
                    return null;

                return (
                    firstPerson.SmoothedHeadPos,
                    firstPerson.SmoothedLeftHandPos,
                    firstPerson.SmoothedRightHandPos,
                    firstPerson.KeypointConfidences[NOSE_INDEX],
                    firstPerson.KeypointConfidences[LEFT_WRIST_INDEX],
                    firstPerson.KeypointConfidences[RIGHT_WRIST_INDEX]
                );
            }
        }

        /// <summary>
        /// Gets the full skeleton data for a specific tracked person by ID.
        /// </summary>
        public (Vector2[] Keypoints, float[] Confidences)? GetFullSkeleton(int personId)
        {
            lock (_syncLock)
            {
                if (!_trackedPeople.TryGetValue(personId, out var person) || !person.HasInitialPositions)
                    return null;

                var keypoints = new Vector2[17];
                var confidences = new float[17];
                Array.Copy(person.SmoothedKeypoints, keypoints, 17);
                Array.Copy(person.KeypointConfidences, confidences, 17);

                return (keypoints, confidences);
            }
        }

        /// <summary>
        /// Gets the array of skeleton connections represented as tuples of integers.
        /// </summary>
        /// <returns>An array of tuples, where each tuple contains two integers representing the indices of connected skeleton
        /// points.</returns>
        public (int,int)[] GetSkeletonConnections()
        {
            return SkeletonConnections;
        }

        /// <summary>
        /// Gets skeleton data for all tracked people.
        /// </summary>
        public IReadOnlyList<(int PersonId, Vector2[] Keypoints, float[] Confidences)> GetAllSkeletons()
        {
            lock (_syncLock)
            {
                var skeletons = new List<(int, Vector2[], float[])>();

                foreach (var person in _trackedPeople.Values)
                {
                    if (!person.HasInitialPositions)
                        continue;

                    var keypoints = new Vector2[17];
                    var confidences = new float[17];
                    Array.Copy(person.SmoothedKeypoints, keypoints, 17);
                    Array.Copy(person.KeypointConfidences, confidences, 17);

                    skeletons.Add((person.PersonId, keypoints, confidences));
                }

                return skeletons;
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
