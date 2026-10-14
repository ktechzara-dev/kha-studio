using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace VisionStudio.DeepLearning.Inference;

/// <summary>
/// Latency performance benchmark metrics for an ONNX Runtime model.
/// </summary>
public sealed record ModelBenchmarkResult(
    string ModelName,
    ExecutionProviderType ActiveProvider,
    int WarmupIterations,
    int TimedIterations,
    double MeanLatencyMs,
    double P50LatencyMs,
    double P95LatencyMs,
    double MinLatencyMs,
    double MaxLatencyMs);

/// <summary>
/// Manages ONNX Runtime inference sessions, hardware execution provider auto-selection,
/// fallback to CPU, warmup runs, and latency profiling.
/// </summary>
public sealed class InferenceSessionManager : IDisposable
{
    private readonly ILogger? _logger;
    private readonly ConcurrentDictionary<string, InferenceSession> _sessions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ExecutionProviderType> _activeProviders = new(StringComparer.Ordinal);
    private bool _isDisposed;

    public InferenceSessionManager(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Creates SessionOptions attempting to configure the preferred Execution Provider.
    /// Gracefully falls back to CPU if hardware accelerator libraries are not available.
    /// </summary>
    public (SessionOptions Options, ExecutionProviderType ConfiguredProvider) CreateSessionOptions(
        ExecutionProviderType preferredProvider)
    {
        var options = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
            IntraOpNumThreads = Environment.ProcessorCount
        };

        if (preferredProvider == ExecutionProviderType.Cpu)
        {
            _logger?.LogDebug("Using default CPU Execution Provider for ONNX Runtime.");
            return (options, ExecutionProviderType.Cpu);
        }

        try
        {
            switch (preferredProvider)
            {
                case ExecutionProviderType.DirectML:
                    try
                    {
                        // DirectML (Windows DirectX 12)
                        // If Microsoft.ML.OnnxRuntime.DirectML is installed, AppendExecutionProvider_DML is available
                        var method = typeof(SessionOptions).GetMethod("AppendExecutionProvider_DML", new[] { typeof(int) });
                        if (method != null)
                        {
                            method.Invoke(options, new object[] { 0 });
                            _logger?.LogInformation("DirectML Execution Provider initialized successfully.");
                            return (options, ExecutionProviderType.DirectML);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "DirectML not available; falling back to CPU.");
                    }
                    break;

                case ExecutionProviderType.Cuda:
                case ExecutionProviderType.TensorRT:
                    try
                    {
                        var method = typeof(SessionOptions).GetMethod("AppendExecutionProvider_CUDA", new[] { typeof(int) });
                        if (method != null)
                        {
                            method.Invoke(options, new object[] { 0 });
                            _logger?.LogInformation("CUDA Execution Provider initialized successfully.");
                            return (options, ExecutionProviderType.Cuda);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "CUDA/TensorRT not available; falling back to CPU.");
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to initialize accelerator {Provider}; falling back to CPU.", preferredProvider);
        }

        _logger?.LogInformation("Execution provider fell back to CPU.");
        return (options, ExecutionProviderType.Cpu);
    }

    /// <summary>
    /// Gets or creates a cached InferenceSession for model bytes.
    /// </summary>
    public InferenceSession GetOrCreateSession(
        string cacheKey,
        byte[] onnxModelBytes,
        ExecutionProviderType preferredProvider = ExecutionProviderType.Cpu)
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(InferenceSessionManager));

        return _sessions.GetOrAdd(cacheKey, _ =>
        {
            var (opts, actualProvider) = CreateSessionOptions(preferredProvider);
            try
            {
                var session = new InferenceSession(onnxModelBytes, opts);
                _activeProviders[cacheKey] = actualProvider;
                _logger?.LogInformation("InferenceSession created for '{Key}' with provider '{Provider}'.", cacheKey, actualProvider);
                return session;
            }
            catch (Exception ex) when (actualProvider != ExecutionProviderType.Cpu)
            {
                _logger?.LogWarning(ex, "Failed to create session with {Provider}; retrying with CPU provider.", actualProvider);
                opts.Dispose();
                var (cpuOpts, _) = CreateSessionOptions(ExecutionProviderType.Cpu);
                var cpuSession = new InferenceSession(onnxModelBytes, cpuOpts);
                _activeProviders[cacheKey] = ExecutionProviderType.Cpu;
                return cpuSession;
            }
        });
    }

    /// <summary>
    /// Gets the active Execution Provider being used by a model session.
    /// </summary>
    public ExecutionProviderType GetActiveProvider(string cacheKey)
    {
        return _activeProviders.TryGetValue(cacheKey, out var p) ? p : ExecutionProviderType.Cpu;
    }

    /// <summary>
    /// Runs a warmup sequence to initialize runtime memory and compiles kernels.
    /// </summary>
    public void Warmup(InferenceSession session, int iterations = 3)
    {
        var inputMeta = session.InputMetadata.First();
        var shape = inputMeta.Value.Dimensions.Select(d => d > 0 ? d : 1).ToArray();
        int totalElements = shape.Aggregate(1, (a, b) => a * b);

        var dummyData = new float[totalElements];
        var tensor = new DenseTensor<float>(dummyData, shape);
        var inputs = new[] { NamedOnnxValue.CreateFromTensor(inputMeta.Key, tensor) };

        for (int i = 0; i < iterations; i++)
        {
            using var results = session.Run(inputs);
        }
    }

    /// <summary>
    /// Benchmarks inference latency of an ONNX session, computing mean, p50, and p95 metrics.
    /// </summary>
    public ModelBenchmarkResult Benchmark(
        string modelName,
        InferenceSession session,
        ExecutionProviderType provider,
        int warmup = 5,
        int timedRuns = 20)
    {
        Warmup(session, warmup);

        var inputMeta = session.InputMetadata.First();
        var shape = inputMeta.Value.Dimensions.Select(d => d > 0 ? d : 1).ToArray();
        int totalElements = shape.Aggregate(1, (a, b) => a * b);
        var dummyData = new float[totalElements];
        var tensor = new DenseTensor<float>(dummyData, shape);

        var latencies = new List<double>(timedRuns);
        var sw = new Stopwatch();

        for (int i = 0; i < timedRuns; i++)
        {
            var inputs = new[] { NamedOnnxValue.CreateFromTensor(inputMeta.Key, tensor) };
            sw.Restart();
            using (var results = session.Run(inputs))
            {
                sw.Stop();
            }
            latencies.Add(sw.Elapsed.TotalMilliseconds);
        }

        latencies.Sort();
        double mean = latencies.Average();
        double p50 = latencies[latencies.Count / 2];
        int p95Index = Math.Min(latencies.Count - 1, (int)Math.Ceiling(latencies.Count * 0.95) - 1);
        double p95 = latencies[p95Index];

        return new ModelBenchmarkResult(
            ModelName: modelName,
            ActiveProvider: provider,
            WarmupIterations: warmup,
            TimedIterations: timedRuns,
            MeanLatencyMs: Math.Round(mean, 2),
            P50LatencyMs: Math.Round(p50, 2),
            P95LatencyMs: Math.Round(p95, 2),
            MinLatencyMs: Math.Round(latencies.First(), 2),
            MaxLatencyMs: Math.Round(latencies.Last(), 2));
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        foreach (var session in _sessions.Values)
        {
            session.Dispose();
        }
        _sessions.Clear();
        _activeProviders.Clear();
    }
}
