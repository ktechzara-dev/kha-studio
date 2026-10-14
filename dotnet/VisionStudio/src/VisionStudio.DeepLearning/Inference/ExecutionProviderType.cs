namespace VisionStudio.DeepLearning.Inference;

/// <summary>
/// Execution provider hardware accelerators for ONNX Runtime inference.
/// </summary>
public enum ExecutionProviderType
{
    /// <summary>
    /// Default cross-platform multi-threaded CPU inference. Always available.
    /// </summary>
    Cpu = 0,

    /// <summary>
    /// DirectML hardware acceleration for Windows DirectX 12 GPUs (Intel, AMD, NVIDIA).
    /// </summary>
    DirectML = 1,

    /// <summary>
    /// NVIDIA CUDA GPU acceleration (requires CUDA and cuDNN runtime libraries).
    /// </summary>
    Cuda = 2,

    /// <summary>
    /// NVIDIA TensorRT highly optimized engine runtime.
    /// </summary>
    TensorRT = 3
}
