namespace PoseIntegrator.Vision.Segmentation;

/// <summary>
/// Execution provider for ONNX inference.
/// </summary>
public enum ExecutionProvider
{
    /// <summary>CPU (default, works everywhere)</summary>
    Cpu,
    /// <summary>DirectML (Windows GPU - AMD/NVIDIA/Intel)</summary>
    DirectML,
    /// <summary>CUDA (NVIDIA GPU only)</summary>
    Cuda
}

/// <summary>
/// Model architecture type for preprocessing/postprocessing.
/// </summary>
public enum ModelType
{
    /// <summary>FCN ResNet-50 (134MB, 21-class Pascal VOC, slower but multi-class)</summary>
    FcnResNet50,
    /// <summary>MediaPipe Selfie Segmentation (256KB, binary person/bg, fast real-time)</summary>
    MediaPipeSelfie,
    /// <summary>PP-HumanSeg (PaddlePaddle, various sizes, good balance)</summary>
    PpHumanSeg,
    /// <summary>Auto-detect based on output shape</summary>
    Auto
}

public sealed record OnnxSegmentationOptions
{
    public required string ModelPath { get; init; }

    // Tensor names vary by model; set these to match your model.
    public string InputTensorName { get; init; } = "input";
    public string OutputTensorName { get; init; } = "output";

    // Model input size - smaller = faster (256 fast, 520 accurate)
    public int InputWidth { get; init; } = 256;
    public int InputHeight { get; init; } = 256;

    // Probability threshold to binarize
    public float Threshold { get; init; } = 0.5f;

    /// <summary>
    /// Execution provider for inference. DirectML recommended for Windows GPU.
    /// </summary>
    public ExecutionProvider ExecutionProvider { get; init; } = ExecutionProvider.Cpu;

    /// <summary>
    /// GPU device ID (for DirectML/CUDA). 0 = first GPU.
    /// </summary>
    public int GpuDeviceId { get; init; } = 0;

    /// <summary>
    /// Model architecture type. Determines preprocessing/postprocessing.
    /// Use MediaPipeSelfie for best real-time person segmentation.
    /// </summary>
    public ModelType ModelType { get; init; } = ModelType.Auto;
}
