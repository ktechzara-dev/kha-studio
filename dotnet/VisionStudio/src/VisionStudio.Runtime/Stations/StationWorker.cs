using Microsoft.Extensions.Logging;
using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Pipeline;
using VisionStudio.Core.Recipes;
using VisionStudio.Core.Tools;
using VisionStudio.Core.Traceability;

namespace VisionStudio.Runtime.Stations;

/// <summary>
/// Active worker thread and inspection context for a single vision station.
/// Runs pipeline execution, records traceability data, and manages hardware or continuous triggers.
/// </summary>
public sealed class StationWorker : IAsyncDisposable
{
    private readonly ILogger? _logger;
    private readonly SemaphoreSlim _executionLock = new(1, 1);
    private CancellationTokenSource? _continuousCts;
    private Task? _continuousTask;

    public StationConfig Config { get; private set; }
    public PipelineGraph Graph { get; private set; }
    public RecipeManager RecipeManager { get; }
    public StationStatistics Statistics { get; } = new();

    public InspectionRecord? LastRecord { get; private set; }
    public byte[]? LastJpegImage { get; private set; }

    public event Action<StationWorker, InspectionRecord>? OnInspectionCompleted;

    public StationWorker(StationConfig config, PipelineGraph graph, RecipeManager? recipeManager = null, ILogger? logger = null)
    {
        Config = config ?? throw new ArgumentNullException(nameof(config));
        Graph = graph ?? throw new ArgumentNullException(nameof(graph));
        RecipeManager = recipeManager ?? new RecipeManager();
        _logger = logger;
    }

    public void Start()
    {
        if (Config.TriggerMode == StationTriggerMode.Continuous)
        {
            _continuousCts = new CancellationTokenSource();
            _continuousTask = Task.Run(() => ContinuousTriggerLoopAsync(_continuousCts.Token));
            _logger?.LogInformation("Station '{Id}' continuous trigger loop started.", Config.StationId);
        }
    }

    public async Task StopAsync()
    {
        if (_continuousCts != null)
        {
            _continuousCts.Cancel();
            if (_continuousTask != null)
            {
                try
                {
                    await _continuousTask;
                }
                catch (OperationCanceledException) { }
            }
            _continuousCts = null;
            _continuousTask = null;
        }
    }

    /// <summary>
    /// Executes a single inspection cycle on this station.
    /// Thread-safe: waits for any currently running inspection to finish.
    /// </summary>
    public async Task<(InspectionRecord Record, PipelineExecutionResult Result)> TriggerAsync(
        Mat? inputImage = null,
        string? recipeName = null,
        CancellationToken ct = default)
    {
        await _executionLock.WaitAsync(ct);
        try
        {
            // Apply recipe if specified
            string activeRecipe = recipeName ?? Config.DefaultRecipe ?? "Default";
            if (!string.IsNullOrEmpty(activeRecipe))
            {
                var recipe = RecipeManager.GetRecipe(activeRecipe)
                    ?? RecipeManager.Recipes.FirstOrDefault(r => string.Equals(r.Name, activeRecipe, StringComparison.OrdinalIgnoreCase));

                if (recipe != null)
                {
                    RecipeManager.ApplyRecipe(recipe, Graph);
                }
            }

            // Synthesize or reuse input image
            Mat workImage = inputImage?.Clone() ?? CreateSyntheticTestImage();
            try
            {
                // Inject input image into any input node/ports
                var externalInputs = BuildExternalInputs(Graph, workImage);

                var executor = new PipelineExecutor(_logger);
                var pipelineResult = await executor.ExecuteAsync(Graph, false, ct, externalInputs);

                // Build InspectionRecord
                var record = BuildInspectionRecord(pipelineResult, activeRecipe);
                Statistics.RecordExecution(record);
                LastRecord = record;

                // Cache preview JPEG
                try
                {
                    LastJpegImage = workImage.ToBytes(".jpg");
                }
                catch
                {
                    LastJpegImage = null;
                }

                OnInspectionCompleted?.Invoke(this, record);
                return (record, pipelineResult);
            }
            finally
            {
                workImage.Dispose();
            }
        }
        finally
        {
            _executionLock.Release();
        }
    }

    /// <summary>
    /// Safely updates the station's pipeline graph and recipe without interrupting ongoing cycles.
    /// </summary>
    public async Task ReloadPipelineAsync(PipelineGraph newGraph, string? defaultRecipe = null, CancellationToken ct = default)
    {
        await _executionLock.WaitAsync(ct);
        try
        {
            Graph = newGraph;
            if (defaultRecipe != null)
            {
                Config.DefaultRecipe = defaultRecipe;
            }
            _logger?.LogInformation("Station '{Id}' pipeline reloaded successfully.", Config.StationId);
        }
        finally
        {
            _executionLock.Release();
        }
    }

    private async Task ContinuousTriggerLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await TriggerAsync(null, null, ct);
                await Task.Delay(Config.ContinuousIntervalMs, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in continuous trigger loop on station '{Id}'", Config.StationId);
                await Task.Delay(1000, ct);
            }
        }
    }

    private static Dictionary<(string, string), object?> BuildExternalInputs(PipelineGraph graph, Mat image)
    {
        var dict = new Dictionary<(string, string), object?>();
        foreach (var node in graph.Nodes)
        {
            foreach (var port in node.Tool.Inputs)
            {
                if (port.Type == VisionStudio.Core.Ports.PortType.Image || port.Name.Equals("Image", StringComparison.OrdinalIgnoreCase))
                {
                    dict[(node.Id, port.Id)] = image;
                }
            }
        }
        return dict;
    }

    private InspectionRecord BuildInspectionRecord(PipelineExecutionResult res, string recipeName)
    {
        bool isPass = res.Success;
        var measurements = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var failureReasons = new List<string>();

        if (!res.Success && !string.IsNullOrEmpty(res.ErrorMessage))
        {
            failureReasons.Add(res.ErrorMessage);
        }

        foreach (var (nodeId, toolRes) in res.NodeResults)
        {
            if (toolRes.Status == ToolStatus.Error)
            {
                isPass = false;
                if (!string.IsNullOrEmpty(toolRes.Message))
                {
                    failureReasons.Add($"Node {nodeId}: {toolRes.Message}");
                }
            }

            foreach (var (key, val) in toolRes.Outputs)
            {
                if (val is double d)
                {
                    measurements[$"{nodeId}_{key}"] = d;
                }
                else if (val is float f)
                {
                    measurements[$"{nodeId}_{key}"] = f;
                }
                else if (val is int i)
                {
                    measurements[$"{nodeId}_{key}"] = i;
                }
            }
        }

        return new InspectionRecord
        {
            Id = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
            Timestamp = DateTimeOffset.UtcNow,
            RecipeName = recipeName,
            PartType = Config.Name,
            Verdict = isPass ? InspectionVerdict.Pass : InspectionVerdict.Fail,
            ExecutionTimeMs = Math.Round(res.TotalTime.TotalMilliseconds, 2),
            Measurements = measurements,
            FailureReasons = failureReasons
        };
    }

    private static Mat CreateSyntheticTestImage()
    {
        var mat = new Mat(480, 640, MatType.CV_8UC3, new Scalar(40, 40, 45));
        Cv2.Circle(mat, new Point(320, 240), 90, new Scalar(0, 180, 255), -1);
        Cv2.Rectangle(mat, new Rect(200, 180, 240, 120), new Scalar(255, 255, 255), 2);
        return mat;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _executionLock.Dispose();
    }
}
