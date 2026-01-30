using System.Numerics;
using PoseIntegrator.Vision.Abstractions;
using PoseIntegrator.Vision.Geometry;
using PoseIntegrator.Vision.Models;
using PoseIntegrator.Vision.Segmentation;

namespace PoseIntegrator.Vision;

/// <summary>
/// Event args containing the detected person polygon data.
/// </summary>
public sealed class PersonDetectedEventArgs : EventArgs
{
    /// <summary>
    /// Convex polygon(s) representing the person silhouette.
    /// Coordinates are normalized 0-1 (multiply by your world size).
    /// </summary>
    public IReadOnlyList<IReadOnlyList<Vector2>> Polygons { get; }

    /// <summary>
    /// Bounding box in pixel coordinates.
    /// </summary>
    public System.Drawing.Rectangle BoundingBox { get; }

    /// <summary>
    /// Detection confidence (0-1).
    /// </summary>
    public float Confidence { get; }

    /// <summary>
    /// Timestamp in milliseconds (UTC).
    /// </summary>
    public long TimestampMs { get; }

    public PersonDetectedEventArgs(PhysicsPolygonsResult result)
    {
        Polygons = result.ConvexPolygons;
        BoundingBox = result.BoundingBoxPixels;
        Confidence = result.Confidence;
        TimestampMs = result.TimestampMs;
    }
}

/// <summary>
/// High-level person detection with background processing.
/// Configured for MediaPipe Selfie Segmentation model only.
/// </summary>
public sealed class PersonDetector : IDisposable
{
    private IFrameSource? _camera;
    private IPersonSegmenter? _segmenter;
    private ConvexDecompositionExtractor? _extractor;

    private Thread? _workerThread;
    private CancellationTokenSource? _cts;
    private volatile bool _isRunning;

    private readonly OnnxSegmentationOptions _segmentationOptions;

    // Polygon extraction parameters
    private readonly double _simplifyEpsilon;
    private readonly double _minDefectDepth;
    private readonly double _minPolygonArea;

    // Temporal smoothing parameters
    private readonly float _smoothingFactor;
    private readonly int _fixedVertexCount;

    private List<Vector2[]>? _previousPolygons;

    /// <summary>
    /// Fired on the background thread when a person is detected.
    /// </summary>
    public event EventHandler<PersonDetectedEventArgs>? OnPersonDetected;

    /// <summary>
    /// Fired when an error occurs during processing.
    /// </summary>
    public event EventHandler<Exception>? OnError;

    /// <summary>
    /// Fired for each frame processed (for debugging/stats).
    /// </summary>
    public event EventHandler<FrameProcessedEventArgs>? OnFrameProcessed;

    /// <summary>
    /// Whether the detector is currently running.
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Creates a new PersonDetector for MediaPipe Selfie Segmentation.
    /// </summary>
    /// <param name="segmentationOptions">ONNX segmentation options (model path, thresholds, etc.).</param>
    /// <param name="simplifyEpsilon">Contour simplification tolerance in pixels (default 1.5).</param>
    /// <param name="minDefectDepth">Minimum convexity defect depth in pixels (default 8.0).</param>
    /// <param name="minPolygonArea">Minimum polygon area as fraction of total (default 0.005).</param>
    /// <param name="smoothingFactor">Temporal smoothing factor (0.0 = no smoothing, 0.9 = very smooth). Default 0.7.</param>
    /// <param name="fixedVertexCount">Fixed number of vertices to resample polygons to for consistent smoothing. Default 64.</param>
    public PersonDetector(
        OnnxSegmentationOptions segmentationOptions,
        double simplifyEpsilon = 1.5,
        double minDefectDepth = 8.0,
        double minPolygonArea = 0.005,
        float smoothingFactor = 0.7f,
        int fixedVertexCount = 64)
    {
        _segmentationOptions = segmentationOptions ?? throw new ArgumentNullException(nameof(segmentationOptions));
        _simplifyEpsilon = simplifyEpsilon;
        _minDefectDepth = minDefectDepth;
        _minPolygonArea = minPolygonArea;
        _smoothingFactor = Math.Clamp(smoothingFactor, 0f, 1f);
        _fixedVertexCount = Math.Max(3, fixedVertexCount);
    }

    ///// <summary>
    ///// Start detection with the specified camera.
    ///// </summary>
    ///// <param name="cameraIndex">Camera index (0 = first camera).</param>
    ///// <param name="width">Frame width (default 640).</param>
    ///// <param name="height">Frame height (default 480).</param>
    ///// <param name="fps">Target FPS (default 30).</param>
    //public void Start(int cameraIndex = 0, int width = 640, int height = 480, int? fps = 30)
    //{
    //    if (_isRunning)
    //        throw new InvalidOperationException("Detector is already running. Call Stop() first.");

    //    _camera = new OpenCvCameraFrameSource(cameraIndex, width, height, fps);
    //    _segmenter = new OnnxPersonSegmenter(_segmentationOptions);
    //    _extractor = new ConvexDecompositionExtractor(_simplifyEpsilon, _minDefectDepth, minPolygonArea: _minPolygonArea);

    //    _cts = new CancellationTokenSource();
    //    _isRunning = true;

    //    _workerThread = new Thread(() => ProcessingLoop(_cts.Token))
    //    {
    //        Name = "PersonDetector-Worker",
    //        IsBackground = true
    //    };
    //    _workerThread.Start();
    //}

    /// <summary>
    /// Start detection with a custom frame source.
    /// </summary>
    /// <param name="frameSource">Custom frame source (you manage its lifetime).</param>
    public void Start(IFrameSource frameSource)
    {
        if (_isRunning)
            throw new InvalidOperationException("Detector is already running. Call Stop() first.");

        _camera = frameSource;
        _segmenter = new OnnxPersonSegmenter(_segmentationOptions);
        _extractor = new ConvexDecompositionExtractor(_simplifyEpsilon, _minDefectDepth, minPolygonArea: _minPolygonArea);

        _cts = new CancellationTokenSource();
        _isRunning = true;

        _workerThread = new Thread(() => ProcessingLoop(_cts.Token))
        {
            Name = "PersonDetector-Worker",
            IsBackground = true
        };
        _workerThread.Start();
    }

    /// <summary>
    /// Stop detection and release resources.
    /// </summary>
    public void Stop()
    {
        if (!_isRunning) return;

        _isRunning = false;
        _cts?.Cancel();

        _workerThread?.Join(TimeSpan.FromSeconds(2));
        _workerThread = null;

        _cts?.Dispose();
        _cts = null;

        _camera?.Dispose();
        _camera = null;

        _segmenter?.Dispose();
        _segmenter = null;

        _extractor = null;
        _previousPolygons = null;
    }

    private void ProcessingLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _isRunning)
        {
            try
            {
                if (_camera is null || _segmenter is null || _extractor is null)
                    break;

                if (!_camera.TryGetFrame(out var frame, out var timestamp))
                {
                    Thread.Sleep(5);
                    continue;
                }

                try
                {
                    var segResult = _segmenter.Segment(frame, timestamp);
                    var physicsResult = _extractor.Extract(segResult);

                    // Apply temporal smoothing if enabled
                    if (_smoothingFactor > 0 && physicsResult.ConvexPolygons.Count > 0)
                    {
                        var smoothedPolygons = ApplyTemporalSmoothing(physicsResult.ConvexPolygons);
                        physicsResult = new PhysicsPolygonsResult(
                            smoothedPolygons,
                            physicsResult.BoundingBoxPixels,
                            physicsResult.Confidence,
                            physicsResult.TimestampMs
                        );
                    }

                    // Notify frame processed
                    OnFrameProcessed?.Invoke(this, new FrameProcessedEventArgs(
                        timestamp,
                        physicsResult.Confidence,
                        physicsResult.ConvexPolygons.Count > 0
                    ));

                    // Only fire event if person detected with valid polygons
                    if (physicsResult.ConvexPolygons.Count > 0 && physicsResult.Confidence > 0)
                    {
                        OnPersonDetected?.Invoke(this, new PersonDetectedEventArgs(physicsResult));
                    }
                }
                finally
                {
                    frame.Dispose();
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                OnError?.Invoke(this, ex);
                Thread.Sleep(100); // Avoid tight error loop
            }
        }
    }

        public void Dispose()
        {
            Stop();
        }

        /// <summary>
        /// Applies temporal smoothing to polygon vertices using exponential moving average.
        /// Resamples polygons to a fixed vertex count for consistent frame-to-frame correspondence.
        /// </summary>
        private IReadOnlyList<IReadOnlyList<Vector2>> ApplyTemporalSmoothing(IReadOnlyList<IReadOnlyList<Vector2>> currentPolygons)
        {
            var smoothedPolygons = new List<IReadOnlyList<Vector2>>();

            // Resample current polygons to fixed vertex count with normalized orientation
            var resampledCurrent = new List<Vector2[]>();
            foreach (var polygon in currentPolygons)
            {
                if (polygon.Count >= 3)
                {
                    var aligned = AlignPolygonOrientation(polygon);
                    resampledCurrent.Add(ResamplePolygon(aligned, _fixedVertexCount));
                }
            }

            // If no previous frame, just return resampled current
            if (_previousPolygons == null || _previousPolygons.Count == 0)
            {
                _previousPolygons = resampledCurrent;
                return resampledCurrent.Select(p => (IReadOnlyList<Vector2>)p).ToList();
            }

            // Match and smooth polygons frame-to-frame
            // For simplicity, we match by index (assumes polygon count is stable)
            // If counts differ, use the minimum count and reset unmatched ones
            int matchCount = Math.Min(_previousPolygons.Count, resampledCurrent.Count);

            for (int i = 0; i < matchCount; i++)
            {
                var smoothed = new Vector2[_fixedVertexCount];

                // Check if vertices have shifted significantly (phase shift detection)
                float totalDistance = 0f;
                for (int v = 0; v < _fixedVertexCount; v++)
                {
                    totalDistance += Vector2.Distance(_previousPolygons[i][v], resampledCurrent[i][v]);
                }
                float avgDistance = totalDistance / _fixedVertexCount;

                // If average distance is too large, try to find the best rotation offset
                if (avgDistance > 0.15f) // Threshold for detecting phase shift (15% of normalized coordinates)
                {
                    resampledCurrent[i] = FindBestVertexAlignment(_previousPolygons[i], resampledCurrent[i]);
                }

                // Apply exponential moving average
                for (int v = 0; v < _fixedVertexCount; v++)
                {
                    var prev = _previousPolygons[i][v];
                    var curr = resampledCurrent[i][v];

                    // Exponential moving average: smoothed = prev * alpha + curr * (1 - alpha)
                    smoothed[v] = new Vector2(
                        prev.X * _smoothingFactor + curr.X * (1 - _smoothingFactor),
                        prev.Y * _smoothingFactor + curr.Y * (1 - _smoothingFactor)
                    );
                }
                smoothedPolygons.Add(smoothed);
                _previousPolygons[i] = smoothed;
            }

            // Handle extra polygons in current frame (new polygons, no smoothing)
            for (int i = matchCount; i < resampledCurrent.Count; i++)
            {
                smoothedPolygons.Add(resampledCurrent[i]);
                if (i < _previousPolygons.Count)
                    _previousPolygons[i] = resampledCurrent[i];
                else
                    _previousPolygons.Add(resampledCurrent[i]);
            }

            // Trim previous polygons if current has fewer
            if (_previousPolygons.Count > resampledCurrent.Count)
            {
                _previousPolygons.RemoveRange(resampledCurrent.Count, _previousPolygons.Count - resampledCurrent.Count);
            }

            return smoothedPolygons;
        }

        /// <summary>
        /// Aligns polygon orientation by rotating vertices so the topmost point is first.
        /// This prevents vertex phase shift when topology changes.
        /// </summary>
        private static Vector2[] AlignPolygonOrientation(IReadOnlyList<Vector2> polygon)
        {
            if (polygon.Count < 3)
                return polygon.ToArray();

            // Find the topmost point (lowest Y value in normalized coords)
            // If multiple points have same Y, use leftmost
            int topIndex = 0;
            float minY = polygon[0].Y;
            float leftmostX = polygon[0].X;

            for (int i = 1; i < polygon.Count; i++)
            {
                if (polygon[i].Y < minY || (Math.Abs(polygon[i].Y - minY) < 0.001f && polygon[i].X < leftmostX))
                {
                    minY = polygon[i].Y;
                    leftmostX = polygon[i].X;
                    topIndex = i;
                }
            }

            // Rotate the polygon so topmost point is first
            var aligned = new Vector2[polygon.Count];
            for (int i = 0; i < polygon.Count; i++)
            {
                aligned[i] = polygon[(topIndex + i) % polygon.Count];
            }

            return aligned;
        }

        /// <summary>
        /// Finds the best vertex rotation offset to align current polygon with previous.
        /// Prevents sudden vertex sliding when topology changes.
        /// </summary>
        private static Vector2[] FindBestVertexAlignment(Vector2[] previous, Vector2[] current)
        {
            if (current.Length != previous.Length)
                return current;

            int vertexCount = current.Length;
            float bestDistance = float.MaxValue;
            int bestOffset = 0;

            // Try all possible rotation offsets
            for (int offset = 0; offset < vertexCount; offset++)
            {
                float totalDistance = 0f;
                for (int i = 0; i < vertexCount; i++)
                {
                    int rotatedIndex = (i + offset) % vertexCount;
                    totalDistance += Vector2.Distance(previous[i], current[rotatedIndex]);
                }

                if (totalDistance < bestDistance)
                {
                    bestDistance = totalDistance;
                    bestOffset = offset;
                }
            }

            // If no rotation is better, return original
            if (bestOffset == 0)
                return current;

            // Rotate vertices to best alignment
            var aligned = new Vector2[vertexCount];
            for (int i = 0; i < vertexCount; i++)
            {
                aligned[i] = current[(i + bestOffset) % vertexCount];
            }

            return aligned;
        }

        /// <summary>
        /// Resamples a polygon to a fixed number of evenly-spaced vertices along its perimeter.
        /// </summary>
        private static Vector2[] ResamplePolygon(IReadOnlyList<Vector2> polygon, int targetVertexCount)
        {
            if (polygon.Count < 3)
                return polygon.ToArray();

            // Calculate total perimeter
            float totalLength = 0f;
            for (int i = 0; i < polygon.Count; i++)
            {
                var p1 = polygon[i];
                var p2 = polygon[(i + 1) % polygon.Count];
                totalLength += Vector2.Distance(p1, p2);
            }

            if (totalLength < 1e-6f)
                return polygon.ToArray();

            // Resample at evenly-spaced intervals
            var resampled = new Vector2[targetVertexCount];
            float segmentLength = totalLength / targetVertexCount;

            int currentEdge = 0;
            float distanceAlongEdge = 0f;

            for (int i = 0; i < targetVertexCount; i++)
            {
                float targetDistance = i * segmentLength;
                float accumulatedDistance = 0f;

                // Find the edge containing this target distance
                currentEdge = 0;
                distanceAlongEdge = 0f;

                for (int e = 0; e < polygon.Count; e++)
                {
                    var p1 = polygon[e];
                    var p2 = polygon[(e + 1) % polygon.Count];
                    float edgeLength = Vector2.Distance(p1, p2);

                    if (accumulatedDistance + edgeLength >= targetDistance)
                    {
                        // Target point is on this edge
                        distanceAlongEdge = targetDistance - accumulatedDistance;
                        float t = edgeLength > 0 ? distanceAlongEdge / edgeLength : 0;
                        resampled[i] = Vector2.Lerp(p1, p2, t);
                        break;
                    }

                    accumulatedDistance += edgeLength;
                }
            }

            return resampled;
        }
    }

/// <summary>
/// Event args for frame processing statistics.
/// </summary>
public sealed class FrameProcessedEventArgs : EventArgs
{
    public long TimestampMs { get; }
    public float Confidence { get; }
    public bool PersonDetected { get; }

    public FrameProcessedEventArgs(long timestampMs, float confidence, bool personDetected)
    {
        TimestampMs = timestampMs;
        Confidence = confidence;
        PersonDetected = personDetected;
    }
}
