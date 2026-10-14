using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Pipeline;
using VisionStudio.Core.Traceability;
using VisionStudio.Runtime.Packaging;
using VisionStudio.Runtime.Stations;

namespace VisionStudio.Runtime.Tests;

public class MultiStationEngineTests : IAsyncDisposable
{
    private readonly MultiStationEngine _engine = new();
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "engine_tests_" + Guid.NewGuid().ToString("N"));

    public MultiStationEngineTests()
    {
        Directory.CreateDirectory(_tempRoot);
    }

    public async ValueTask DisposeAsync()
    {
        await _engine.DisposeAsync();
        if (Directory.Exists(_tempRoot))
        {
            try { Directory.Delete(_tempRoot, true); } catch { }
        }
    }

    [Fact]
    public async Task MultiStationEngine_CanRegisterAndTriggerStations()
    {
        var graph1 = new PipelineGraph();
        var graph2 = new PipelineGraph();

        var station1 = new StationWorker(new StationConfig { StationId = "Station_A", Name = "Head A" }, graph1);
        var station2 = new StationWorker(new StationConfig { StationId = "Station_B", Name = "Head B" }, graph2);

        _engine.RegisterStation(station1);
        _engine.RegisterStation(station2);

        _engine.Stations.Should().HaveCount(2);

        // Trigger Station A
        var (recA, _) = await _engine.TriggerStationAsync("Station_A");
        recA.Should().NotBeNull();
        recA.Verdict.Should().Be(InspectionVerdict.Pass);
        station1.Statistics.TotalCount.Should().Be(1);
        station1.Statistics.PassCount.Should().Be(1);

        // Trigger Station B
        var (recB, _) = await _engine.TriggerStationAsync("Station_B");
        recB.Should().NotBeNull();
        station2.Statistics.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task MultiStationEngine_FiresInspectionCompletedEvent()
    {
        var station = new StationWorker(new StationConfig { StationId = "Station_Event" }, new PipelineGraph());
        _engine.RegisterStation(station);

        InspectionRecord? captured = null;
        _engine.OnInspectionCompleted += (w, r) => captured = r;

        await _engine.TriggerStationAsync("Station_Event");

        captured.Should().NotBeNull();
        captured!.Verdict.Should().Be(InspectionVerdict.Pass);
    }

    [Fact]
    public async Task MultiStationEngine_GracefullyReloadsPackage()
    {
        // Setup initial station
        var station1 = new StationWorker(new StationConfig { StationId = "Line1_Head1" }, new PipelineGraph());
        _engine.RegisterStation(station1);

        // Create a deployed package folder with manifest
        string deployDir = Path.Combine(_tempRoot, "deployed_pkg");
        Directory.CreateDirectory(deployDir);

        var graph = new PipelineGraph();
        var serializer = new PipelineSerializer();
        string pipeJson = serializer.SerializeToJson(graph);
        File.WriteAllText(Path.Combine(deployDir, "pipeline_v2.json"), pipeJson);

        var manifest = new PackageManifest
        {
            Version = "2.0.0",
            Stations = new List<StationMappingEntry>
            {
                new() { StationId = "Line1_Head1", PipelineFile = "pipeline_v2.json", DefaultRecipe = "Recipe_HighSpeed" },
                new() { StationId = "Line1_Head2", PipelineFile = "pipeline_v2.json" }
            }
        };
        File.WriteAllText(Path.Combine(deployDir, "manifest.json"), manifest.ToJson());

        int reloaded = await _engine.GracefulReloadPackageAsync(deployDir);
        reloaded.Should().Be(2);

        // Check updated default recipe on Head1
        _engine.GetStation("Line1_Head1")?.Config.DefaultRecipe.Should().Be("Recipe_HighSpeed");

        // Check newly added Head2
        _engine.GetStation("Line1_Head2").Should().NotBeNull();
    }
}
