using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;
using PoseIntegrator.Vision.Abstractions;

namespace PoseIntegrator.Vision.FrameSources;

/// <summary>
/// Frame source that reads from an MJPEG HTTP stream (e.g., IP cameras, ESP32-CAM).
/// </summary>
public sealed class MjpegCameraFrameSource : IFrameSource
{
    private readonly string _url;
    private readonly int _maxQueueSize;
    private readonly ConcurrentQueue<(Mat Frame, long Timestamp)> _frameQueue = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly HttpClient _httpClient;
    private Task? _streamTask;
    private volatile bool _isRunning;
    private volatile int _queueSize;

    /// <summary>
    /// Creates a new MJPEG camera frame source.
    /// </summary>
    /// <param name="url">The URL of the MJPEG stream (e.g., "http://192.168.1.100:81/stream").</param>
    /// <param name="maxQueueSize">Maximum frames to buffer before discarding old frames. Default 5.</param>
    /// <param name="autoStart">Whether to start streaming immediately. Default true.</param>
    public MjpegCameraFrameSource(string url, int maxQueueSize = 5, bool autoStart = true)
    {
        _url = url ?? throw new ArgumentNullException(nameof(url));
        _maxQueueSize = Math.Max(1, maxQueueSize);
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        if (autoStart)
        {
            Start();
        }
    }

    /// <summary>
    /// Gets whether the stream is currently running.
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Gets the current number of frames in the queue.
    /// </summary>
    public int QueuedFrameCount => _queueSize;

    /// <summary>
    /// Starts streaming from the MJPEG URL.
    /// </summary>
    public void Start()
    {
        if (_isRunning) return;

        _isRunning = true;
        _streamTask = Task.Run(() => StreamLoop(_cts.Token));
        Console.WriteLine($"[MjpegSource] Started streaming from: {_url}");
    }

    /// <summary>
    /// Stops streaming.
    /// </summary>
    public void Stop()
    {
        if (!_isRunning) return;

        _isRunning = false;

        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed
        }
    }

    /// <summary>
    /// Background task that continuously reads frames from the MJPEG stream.
    /// </summary>
    private async Task StreamLoop(CancellationToken ct)
    {
        var frameBuffer = new List<byte>(64 * 1024); // Pre-allocate 64KB buffer
        var readBuffer = new byte[8192];

        while (!ct.IsCancellationRequested && _isRunning)
        {
            try
            {
                using var response = await _httpClient.GetAsync(_url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);

                Console.WriteLine("[MjpegSource] Connected to stream");

                while (!ct.IsCancellationRequested && _isRunning)
                {
                    int bytesRead = await stream.ReadAsync(readBuffer, 0, readBuffer.Length, ct).ConfigureAwait(false);
                    if (bytesRead <= 0) break;

                    for (int i = 0; i < bytesRead; i++)
                    {
                        frameBuffer.Add(readBuffer[i]);

                        // Check for JPEG End of Image (EOI) marker: 0xFF 0xD9
                        if (frameBuffer.Count > 1 &&
                            frameBuffer[^2] == 0xFF &&
                            frameBuffer[^1] == 0xD9)
                        {
                            // Found complete JPEG frame
                            ProcessJpegFrame(frameBuffer);
                            frameBuffer.Clear();
                        }
                    }

                    // Prevent buffer from growing too large (protect against malformed streams)
                    if (frameBuffer.Count > 10 * 1024 * 1024) // 10MB limit
                    {
                        Console.WriteLine("[MjpegSource] Warning: Buffer overflow, clearing");
                        frameBuffer.Clear();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation (includes TaskCanceledException) - exit silently
                break;
            }
            catch (HttpRequestException) when (ct.IsCancellationRequested || !_isRunning)
            {
                // HTTP error during cancellation - exit silently
                break;
            }
            catch (Exception ex)
            {
                if (ct.IsCancellationRequested || !_isRunning)
                {
                    // Shutting down, ignore errors
                    break;
                }

                Console.WriteLine($"[MjpegSource] Stream error: {ex.Message}");

                // Wait before reconnecting
                try
                {
                    await Task.Delay(4000, ct).ConfigureAwait(false);
                    Console.WriteLine("[MjpegSource] Attempting to reconnect...");
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        Console.WriteLine("[MjpegSource] Stream loop ended");
    }

    /// <summary>
    /// Processes a complete JPEG frame from the buffer.
    /// </summary>
    private void ProcessJpegFrame(List<byte> frameBuffer)
    {
        try
        {
            // Find JPEG Start of Image (SOI) marker: 0xFF 0xD8
            int startIndex = -1;
            for (int i = 0; i < frameBuffer.Count - 1; i++)
            {
                if (frameBuffer[i] == 0xFF && frameBuffer[i + 1] == 0xD8)
                {
                    startIndex = i;
                    break;
                }
            }

            if (startIndex < 0) return;

            // Extract JPEG data
            var jpegData = new byte[frameBuffer.Count - startIndex];
            for (int i = 0; i < jpegData.Length; i++)
            {
                jpegData[i] = frameBuffer[startIndex + i];
            }

            // Decode to OpenCV Mat
            var frame = Cv2.ImDecode(jpegData, ImreadModes.Color);

            if (frame == null || frame.Empty())
            {
                frame?.Dispose();
                return;
            }

            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // Manage queue size - discard old frames if queue is full
            while (_queueSize >= _maxQueueSize && _frameQueue.TryDequeue(out var oldFrame))
            {
                oldFrame.Frame.Dispose();
                Interlocked.Decrement(ref _queueSize);
            }

            // Enqueue new frame
            _frameQueue.Enqueue((frame, timestamp));
            Interlocked.Increment(ref _queueSize);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MjpegSource] Frame decode error: {ex.Message}");
        }
    }

    /// <summary>
    /// Tries to get the next frame from the queue.
    /// </summary>
    public bool TryGetFrame(out Mat bgr, out long timestampMs)
    {
        if (_frameQueue.TryDequeue(out var frameData))
        {
            Interlocked.Decrement(ref _queueSize);
            bgr = frameData.Frame;
            timestampMs = frameData.Timestamp;
            return true;
        }

        bgr = default!;
        timestampMs = 0;
        return false;
    }

        /// <summary>
        /// Disposes resources and stops streaming.
        /// </summary>
        public void Dispose()
        {
            Stop();

            // Wait for stream task to complete
            if (_streamTask != null)
            {
                try
                {
                    // Use a short timeout - if it doesn't stop, force ahead
                    if (!_streamTask.Wait(TimeSpan.FromSeconds(2)))
                    {
                        Console.WriteLine("[MjpegSource] Warning: Stream task did not complete in time");
                    }
                }
                catch (AggregateException)
                {
                    // Task was cancelled or faulted - this is expected
                }
                catch (ObjectDisposedException)
                {
                    // Already disposed
                }
            }

            try
            {
                _cts.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed
            }

            try
            {
                _httpClient.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed
            }

            // Clear and dispose remaining frames
            while (_frameQueue.TryDequeue(out var frame))
            {
                try
                {
                    frame.Frame.Dispose();
                }
                catch
                {
                    // Ignore disposal errors
                }
            }

            Console.WriteLine("[MjpegSource] Disposed");
        }
    }
