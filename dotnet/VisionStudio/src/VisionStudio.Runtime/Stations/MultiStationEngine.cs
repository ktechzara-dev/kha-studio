using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using VisionStudio.Core.Pipeline;
using VisionStudio.Core.Tools;
using VisionStudio.Core.Traceability;
using VisionStudio.Runtime.Packaging;

namespace VisionStudio.Runtime.Stations;

/// <summary>
/// Industrial multi-station execution engine managing multiple camera inspection heads
/// in parallel with graceful, zero-downtime hot reloading.
/// </summary>
public sealed class MultiStationEngine : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, StationWorker> _stations = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger? _logger;
    private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;

    public TimeSpan Uptime => DateTimeOffset.UtcNow - _startedAt;
    public IReadOnlyCollection<StationWorker> Stations => _stations.Values.ToList();

    public event Action<StationWorker, InspectionRecord>? OnInspectionCompleted;

    public MultiStationEngine(ILogger? logger = null)
    {
        _logger = logger;
    }

    public void RegisterStation(StationWorker worker)
    {
        if (worker == null) throw new ArgumentNullException(nameof(worker));
        _stations[worker.Config.StationId] = worker;
        worker.OnInspectionCompleted += (w, r) => OnInspectionCompleted?.Invoke(w, r);
        worker.Start();
        _logger?.LogInformation("Registered Station '{Id}' ({Name})", worker.Config.StationId, worker.Config.Name);
    }

    public async Task<bool> UnregisterStationAsync(string stationId)
    {
        if (_stations.TryRemove(stationId, out var worker))
        {
            await worker.StopAsync();
            await worker.DisposeAsync();
            _logger?.LogInformation("Unregistered Station '{Id}'", stationId);
            return true;
        }
        return false;
    }

    public StationWorker? GetStation(string stationId)
    {
        _stations.TryGetValue(stationId, out var worker);
        return worker;
    }

    public async Task<(InspectionRecord Record, PipelineExecutionResult Result)> TriggerStationAsync(
        string stationId,
        Mat? inputImage = null,
        string? recipeName = null,
        CancellationToken ct = default)
    {
        var station = GetStation(stationId)
            ?? throw new KeyNotFoundException($"Station '{stationId}' not found in runtime.");

        return await station.TriggerAsync(inputImage, recipeName, ct);
    }

    /// <summary>
    /// Performs graceful hot-reload of pipelines and recipes across all stations from a deployed package folder.
    /// Does not stop or abort already executing inspection triggers on idle lines.
    /// </summary>
    public async Task<int> GracefulReloadPackageAsync(string deployedDir, CancellationToken ct = default)
    {
        string manifestPath = Path.Combine(deployedDir, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException($"Package manifest not found at '{manifestPath}'");
        }

        string manifestJson = await File.ReadAllTextAsync(manifestPath, Encoding.UTF8, ct);
        var manifest = PackageManifest.FromJson(manifestJson)
            ?? throw new InvalidDataException("Failed to parse package manifest during graceful reload.");

        int reloadedCount = 0;
        foreach (var mapping in manifest.Stations)
        {
            string pipelineFile = Path.Combine(deployedDir, mapping.PipelineFile);
            if (!File.Exists(pipelineFile))
            {
                _logger?.LogWarning("Pipeline file '{File}' for station '{Station}' not found.", pipelineFile, mapping.StationId);
                continue;
            }

            string pipelineJson = await File.ReadAllTextAsync(pipelineFile, Encoding.UTF8, ct);
            var serializer = new PipelineSerializer();
            var graph = serializer.DeserializeFromJson(pipelineJson, ToolRegistry.Shared);

            var existing = GetStation(mapping.StationId);
            if (existing != null)
            {
                await existing.ReloadPipelineAsync(graph, mapping.DefaultRecipe, ct);
                reloadedCount++;
            }
            else
            {
                var config = new StationConfig
                {
                    StationId = mapping.StationId,
                    Name = $"Station {mapping.StationId}",
                    PipelineFile = mapping.PipelineFile,
                    DefaultRecipe = mapping.DefaultRecipe
                };
                var newWorker = new StationWorker(config, graph, null, _logger);
                RegisterStation(newWorker);
                reloadedCount++;
            }
        }

        _logger?.LogInformation("Gracefully reloaded {Count} stations from package v{Version}", reloadedCount, manifest.Version);
        return reloadedCount;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var station in _stations.Values)
        {
            await station.StopAsync();
            await station.DisposeAsync();
        }
        _stations.Clear();
    }
}
