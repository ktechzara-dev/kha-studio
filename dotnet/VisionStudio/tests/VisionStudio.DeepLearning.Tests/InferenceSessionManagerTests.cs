using Microsoft.ML.OnnxRuntime;
using VisionStudio.DeepLearning.Inference;
using VisionStudio.DeepLearning.Synthetic;

namespace VisionStudio.DeepLearning.Tests;

public sealed class InferenceSessionManagerTests
{
    [Fact]
    public void SessionManager_CreatesSessionAndPerformsWarmupAndBenchmark()
    {
        var (meta, onnxBytes) = SyntheticOnnxModelGenerator.CreateClassificationSample(numClasses: 3);

        using var manager = new InferenceSessionManager();
        var session = manager.GetOrCreateSession("sample_classifier", onnxBytes, ExecutionProviderType.Cpu);

        session.Should().NotBeNull();
        session.InputMetadata.Should().ContainKey(meta.InputName);

        // Warmup
        manager.Warmup(session, iterations: 2);

        // Latency Benchmark
        var benchmark = manager.Benchmark("SyntheticClassifier", session, ExecutionProviderType.Cpu, warmup: 2, timedRuns: 10);
        benchmark.Should().NotBeNull();
        benchmark.MeanLatencyMs.Should().BeGreaterThan(0.0);
        benchmark.P50LatencyMs.Should().BeGreaterThan(0.0);
        benchmark.P95LatencyMs.Should().BeGreaterThan(0.0);
    }

    [Fact]
    public void SessionManager_FallsBackToCpu_WhenHardwareAcceleratorUnavailable()
    {
        var (meta, onnxBytes) = SyntheticOnnxModelGenerator.CreateClassificationSample(numClasses: 3);

        using var manager = new InferenceSessionManager();
        // Request CUDA on machine without native CUDA libraries
        var session = manager.GetOrCreateSession("fallback_test", onnxBytes, ExecutionProviderType.Cuda);

        session.Should().NotBeNull();
        manager.GetActiveProvider("fallback_test").Should().Be(ExecutionProviderType.Cpu);
    }
}
