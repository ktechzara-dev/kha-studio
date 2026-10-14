using VisionStudio.Core.Pipeline;
using VisionStudio.Runtime.Metrics;
using VisionStudio.Runtime.Stations;

namespace VisionStudio.Runtime.Tests;

public class PrometheusMetricsTests
{
    [Fact]
    public async Task PrometheusExporter_GeneratesValidPrometheusMetrics()
    {
        var engine = new MultiStationEngine();
        var station = new StationWorker(new StationConfig { StationId = "St_Metric_01" }, new PipelineGraph());
        engine.RegisterStation(station);

        await station.TriggerAsync();

        string metrics = PrometheusExporter.GenerateMetrics(engine);

        metrics.Should().Contain("visionstudio_uptime_seconds");
        metrics.Should().Contain("visionstudio_stations_count 1");
        metrics.Should().Contain("visionstudio_inspections_total{station=\"St_Metric_01\"} 1");
        metrics.Should().Contain("visionstudio_inspections_pass{station=\"St_Metric_01\"} 1");
        metrics.Should().Contain("visionstudio_inspections_fail{station=\"St_Metric_01\"} 0");
        metrics.Should().Contain("visionstudio_cycle_time_ms{station=\"St_Metric_01\"}");
        metrics.Should().Contain("visionstudio_yield_percentage{station=\"St_Metric_01\"} 100");
    }
}
