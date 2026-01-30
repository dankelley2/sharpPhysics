using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using PoseIntegrator.Vision.Abstractions;
using PoseIntegrator.Vision.Models;

namespace PoseIntegrator.Vision.Segmentation;

public sealed class OnnxPersonSegmenter : IPersonSegmenter
{
    private readonly InferenceSession _session;
    private readonly OnnxSegmentationOptions _opt;
    private ModelType _detectedModelType;
    private readonly string _inputName;
    private readonly string _outputName;
    private readonly int[] _inputShape;

    public OnnxPersonSegmenter(OnnxSegmentationOptions options)
    {
        _opt = options ?? throw new ArgumentNullException(nameof(options));
        if (!File.Exists(_opt.ModelPath))
            throw new FileNotFoundException("ONNX model file not found.", _opt.ModelPath);

        var sessionOptions = new SessionOptions();
        
        switch (_opt.ExecutionProvider)
        {
            case ExecutionProvider.DirectML:
                // Windows GPU (AMD/NVIDIA/Intel) - requires Microsoft.ML.OnnxRuntime.DirectML
                sessionOptions.AppendExecutionProvider_DML(_opt.GpuDeviceId);
                break;
            case ExecutionProvider.Cuda:
                // NVIDIA GPU - requires Microsoft.ML.OnnxRuntime.Gpu
                sessionOptions.AppendExecutionProvider_CUDA(_opt.GpuDeviceId);
                break;
            case ExecutionProvider.Cpu:
            default:
                // CPU fallback (always works)
                break;
        }
        
        _session = new InferenceSession(_opt.ModelPath, sessionOptions);
        
        // Auto-detect tensor names from model metadata
        var inputMeta = _session.InputMetadata.First();
        var outputMeta = _session.OutputMetadata.First();
        
        _inputName = string.IsNullOrEmpty(_opt.InputTensorName) || _opt.InputTensorName == "input" 
            ? inputMeta.Key 
            : _opt.InputTensorName;
        _outputName = string.IsNullOrEmpty(_opt.OutputTensorName) || _opt.OutputTensorName == "output" 
            ? outputMeta.Key 
            : _opt.OutputTensorName;
        _inputShape = inputMeta.Value.Dimensions;
        
        // Auto-detect model type based on input shape
        _detectedModelType = _opt.ModelType == ModelType.Auto ? DetectModelType() : _opt.ModelType;
    }

    public SegmentationResult Segment(Mat bgr, long timestampMs)
    {
        if (bgr is null || bgr.Empty())
            throw new ArgumentException("Input frame is null/empty.", nameof(bgr));

        int origW = bgr.Width;
        int origH = bgr.Height;

        // Resize to model input size
        using var resized = new Mat();
        Cv2.Resize(bgr, resized, new Size(_opt.InputWidth, _opt.InputHeight), 0, 0, InterpolationFlags.Linear);

        // Build input tensor based on model type
        DenseTensor<float> input;
        
        if (_detectedModelType == ModelType.MediaPipeSelfie)
        {
            // MediaPipe Selfie Segmentation: RGB, 0-1 range, no normalization, NHWC format
            input = PrepareInputMediaPipe(resized);
        }
        else
        {
            // FCN / ImageNet models: RGB, ImageNet normalization, NCHW format
            input = PrepareInputImageNet(resized);
        }

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(_inputName, input)
        };

        using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _session.Run(inputs);

        // Fetch output tensor using auto-detected name
        var output = results.FirstOrDefault(r => r.Name == _outputName)?.AsTensor<float>()
                     ?? results.First().AsTensor<float>(); // Fallback to first output

        // Process output based on model type and shape
        return ProcessOutput(output, origW, origH, timestampMs, _detectedModelType);
    }

    private DenseTensor<float> PrepareInputMediaPipe(Mat resized)
    {
        // MediaPipe Selfie Segmentation expects RGB 0-1 normalized
        using var rgb = new Mat();
        Cv2.CvtColor(resized, rgb, ColorConversionCodes.BGR2RGB);

        var input = new DenseTensor<float>(new[] { 1, _opt.InputHeight, _opt.InputWidth, 3 }); // NHWC format

        for (int y = 0; y < _opt.InputHeight; y++)
        {
            for (int x = 0; x < _opt.InputWidth; x++)
            {
                Vec3b px = rgb.At<Vec3b>(y, x);
                input[0, y, x, 0] = px.Item0 / 255f; // R
                input[0, y, x, 1] = px.Item1 / 255f; // G
                input[0, y, x, 2] = px.Item2 / 255f; // B
            }
        }

        return input;
    }

    private DenseTensor<float> PrepareInputImageNet(Mat resized)
    {
        // FCN/ImageNet models expect RGB with ImageNet normalization, NCHW format
        using var rgb = new Mat();
        Cv2.CvtColor(resized, rgb, ColorConversionCodes.BGR2RGB);

        var input = new DenseTensor<float>(new[] { 1, 3, _opt.InputHeight, _opt.InputWidth }); // NCHW format

        // ImageNet normalization values
        float[] mean = { 0.485f, 0.456f, 0.406f };
        float[] std = { 0.229f, 0.224f, 0.225f };

        for (int y = 0; y < _opt.InputHeight; y++)
        {
            for (int x = 0; x < _opt.InputWidth; x++)
            {
                Vec3b px = rgb.At<Vec3b>(y, x);
                input[0, 0, y, x] = (px.Item0 / 255f - mean[0]) / std[0]; // R
                input[0, 1, y, x] = (px.Item1 / 255f - mean[1]) / std[1]; // G
                input[0, 2, y, x] = (px.Item2 / 255f - mean[2]) / std[2]; // B
            }
        }

        return input;
    }

    private ModelType DetectModelType()
    {
        // Auto-detect based on input/output names or shapes
        var inputMeta = _session.InputMetadata;
        var outputMeta = _session.OutputMetadata;

        // Check input shape - MediaPipe uses NHWC [1,H,W,3], FCN uses NCHW [1,3,H,W]
        if (_inputShape.Length == 4)
        {
            // If last dim is 3 (RGB channels), it's NHWC (MediaPipe style)
            if (_inputShape[3] == 3)
            {
                _detectedModelType = ModelType.MediaPipeSelfie;
                return ModelType.MediaPipeSelfie;
            }
            // If second dim is 3, it's NCHW (FCN style)  
            if (_inputShape[1] == 3)
            {
                _detectedModelType = ModelType.FcnResNet50;
                return ModelType.FcnResNet50;
            }
        }

        // MediaPipe models often have specific input names
        if (inputMeta.ContainsKey("input_1") || inputMeta.ContainsKey("sub_7"))
        {
            _detectedModelType = ModelType.MediaPipeSelfie;
            return ModelType.MediaPipeSelfie;
        }

        // FCN models have "input" and "out" with 21 classes
        if (outputMeta.TryGetValue(_outputName, out var outInfo))
        {
            var dims = outInfo.Dimensions;
            if (dims.Length == 4 && dims[1] == 21)
            {
                _detectedModelType = ModelType.FcnResNet50;
                return ModelType.FcnResNet50;
            }
        }

        // Default to FCN for backward compatibility
        _detectedModelType = ModelType.FcnResNet50;
        return ModelType.FcnResNet50;
    }

    private SegmentationResult ProcessOutput(Tensor<float> output, int origW, int origH, long timestampMs, ModelType modelType)
    {
        int outRank = output.Rank;
        int outH, outW;
        
        // Determine output dimensions based on rank and model type
        if (outRank == 4)
        {
            // Could be NCHW [1,C,H,W] or NHWC [1,H,W,C]
            // For MediaPipe, it's always NHWC with last dim = 1
            // For FCN, it's NCHW with second dim = 21
            if (modelType == ModelType.MediaPipeSelfie || output.Dimensions[3] <= 2)
            {
                // NHWC format: [1, H, W, C]
                outH = output.Dimensions[1];
                outW = output.Dimensions[2];
            }
            else
            {
                // NCHW format: [1, C, H, W]
                outH = output.Dimensions[2];
                outW = output.Dimensions[3];
            }
        }
        else if (outRank == 3)
        {
            // [1,H,W] or [H,W,C]
            outH = output.Dimensions[1];
            outW = output.Dimensions.Length > 2 ? output.Dimensions[2] : output.Dimensions[1];
        }
        else
        {
            throw new NotSupportedException($"Unsupported output rank: {outRank}.");
        }

        var modelMask = new byte[outH * outW];
        float sumProb = 0;
        int count = 0;

        // For MediaPipe, compute min/max for normalization first
        float minVal = 0, maxVal = 1;
        if (modelType == ModelType.MediaPipeSelfie)
        {
            (minVal, maxVal) = ComputeMinMax(output, outH, outW, modelType);
        }

        for (int y = 0; y < outH; y++)
        {
            for (int x = 0; x < outW; x++)
            {
                float p = ExtractProbability(output, y, x, modelType, minVal, maxVal);
                
                sumProb += p;
                count++;
                modelMask[y * outW + x] = (p >= _opt.Threshold) ? (byte)255 : (byte)0;
            }
        }

        float confidence = count > 0 ? (sumProb / count) : 0f;

        // Resize mask back to original resolution
        using var maskMat = new Mat(outH, outW, MatType.CV_8UC1);
        maskMat.SetArray(modelMask);
        using var resizedMask = new Mat();
        Cv2.Resize(maskMat, resizedMask, new Size(origW, origH), 0, 0, InterpolationFlags.Nearest);

        var outMask = new byte[origW * origH];
        resizedMask.GetArray(out outMask);

        return new SegmentationResult(origW, origH, confidence, outMask, timestampMs);
    }

    private (float min, float max) ComputeMinMax(Tensor<float> output, int outH, int outW, ModelType modelType)
    {
        float minVal = float.MaxValue;
        float maxVal = float.MinValue;
        
        for (int y = 0; y < outH; y++)
        {
            for (int x = 0; x < outW; x++)
            {
                float v = GetRawValue(output, y, x, modelType);
                if (v < minVal) minVal = v;
                if (v > maxVal) maxVal = v;
            }
        }
        
        return (minVal, maxVal);
    }

    private float GetRawValue(Tensor<float> output, int y, int x, ModelType modelType)
    {
        int outRank = output.Rank;
        
        if (modelType == ModelType.MediaPipeSelfie)
        {
            // MediaPipe outputs in NHWC [1,H,W,1]
            if (outRank == 4)
            {
                if (output.Dimensions[3] == 1 || output.Dimensions[3] == 2)
                {
                    // NHWC: [1,H,W,C]
                    return output.Dimensions[3] == 2 ? output[0, y, x, 1] : output[0, y, x, 0];
                }
                return output[0, 0, y, x]; // NCHW fallback
            }
            return output[0, y, x];
        }
        
        // For other models, use standard access
        return outRank == 4 ? output[0, 0, y, x] : output[0, y, x];
    }

    private float ExtractProbability(Tensor<float> output, int y, int x, ModelType modelType, float minVal = 0, float maxVal = 1)
    {
        int outRank = output.Rank;
        float p;

        if (modelType == ModelType.MediaPipeSelfie)
        {
            // Get raw value
            float raw = GetRawValue(output, y, x, modelType);
            
            // Apply min-max normalization (as per Python demo)
            float range = maxVal - minVal;
            p = range > 0.0001f ? (raw - minVal) / range : 0f;
            
            return Math.Clamp(p, 0f, 1f);
        }
        else if (modelType == ModelType.FcnResNet50)
        {
            // FCN outputs 21 classes, class 15 is "person" (Pascal VOC)
            const int personClassIdx = 15;
            const int numClasses = 21;

            if (outRank == 4 && output.Dimensions[1] == numClasses)
            {
                // Apply softmax across classes
                float maxLogit = float.MinValue;
                for (int c = 0; c < numClasses; c++)
                {
                    float logit = output[0, c, y, x];
                    if (logit > maxLogit) maxLogit = logit;
                }

                float sumExp = 0;
                for (int c = 0; c < numClasses; c++)
                {
                    sumExp += MathF.Exp(output[0, c, y, x] - maxLogit);
                }

                p = MathF.Exp(output[0, personClassIdx, y, x] - maxLogit) / sumExp;
            }
            else
            {
                // Binary output
                p = outRank == 4 ? output[0, 0, y, x] : output[0, y, x];
                if (p < 0 || p > 1)
                    p = 1f / (1f + MathF.Exp(-p)); // sigmoid
            }
        }
        else
        {
            // Generic binary segmentation
            p = outRank == 4 ? output[0, 0, y, x] : output[0, y, x];
            if (p < 0 || p > 1)
                p = 1f / (1f + MathF.Exp(-p)); // sigmoid
        }

        return p;
    }

    public void Dispose() => _session.Dispose();
}
