using System.Text;
using VisionStudio.Runtime.Stations;

namespace VisionStudio.Runtime.Metrics;

/// <summary>
/// Exports industrial runtime statistics and station KPI metrics in official Prometheus scrape format.
/// </summary>
public static class PrometheusExporter
{
    public static string GenerateMetrics(MultiStationEngine engine)
    {
        var sb = new StringBuilder();

        // System gauges
        sb.AppendLine("# HELP visionstudio_uptime_seconds Runtime daemon uptime in seconds");
        sb.AppendLine("# TYPE visionstudio_uptime_seconds gauge");
        sb.AppendLine($"visionstudio_uptime_seconds {engine.Uptime.TotalSeconds:F1}");
        sb.AppendLine();

        sb.AppendLine("# HELP visionstudio_stations_count Number of registered camera inspection stations");
        sb.AppendLine("# TYPE visionstudio_stations_count gauge");
        sb.AppendLine($"visionstudio_stations_count {engine.Stations.Count}");
        sb.AppendLine();

        // Station counters and gauges
        sb.AppendLine("# HELP visionstudio_inspections_total Cumulative number of inspections");
        sb.AppendLine("# TYPE visionstudio_inspections_total counter");
        foreach (var station in engine.Stations)
        {
            sb.AppendLine($"visionstudio_inspections_total{{station=\"{station.Config.StationId}\"}} {station.Statistics.TotalCount}");
        }
        sb.AppendLine();

        sb.AppendLine("# HELP visionstudio_inspections_pass Cumulative number of passed inspections");
        sb.AppendLine("# TYPE visionstudio_inspections_pass counter");
        foreach (var station in engine.Stations)
        {
            sb.AppendLine($"visionstudio_inspections_pass{{station=\"{station.Config.StationId}\"}} {station.Statistics.PassCount}");
        }
        sb.AppendLine();

        sb.AppendLine("# HELP visionstudio_inspections_fail Cumulative number of failed inspections");
        sb.AppendLine("# TYPE visionstudio_inspections_fail counter");
        foreach (var station in engine.Stations)
        {
            sb.AppendLine($"visionstudio_inspections_fail{{station=\"{station.Config.StationId}\"}} {station.Statistics.FailCount}");
        }
        sb.AppendLine();

        sb.AppendLine("# HELP visionstudio_cycle_time_ms Last inspection cycle time in milliseconds");
        sb.AppendLine("# TYPE visionstudio_cycle_time_ms gauge");
        foreach (var station in engine.Stations)
        {
            sb.AppendLine($"visionstudio_cycle_time_ms{{station=\"{station.Config.StationId}\"}} {station.Statistics.LastCycleTimeMs:F2}");
        }
        sb.AppendLine();

        sb.AppendLine("# HELP visionstudio_cycle_time_avg_ms Moving average cycle time in milliseconds");
        sb.AppendLine("# TYPE visionstudio_cycle_time_avg_ms gauge");
        foreach (var station in engine.Stations)
        {
            sb.AppendLine($"visionstudio_cycle_time_avg_ms{{station=\"{station.Config.StationId}\"}} {station.Statistics.AverageCycleTimeMs:F2}");
        }
        sb.AppendLine();

        sb.AppendLine("# HELP visionstudio_fps Inspection throughput frames per second");
        sb.AppendLine("# TYPE visionstudio_fps gauge");
        foreach (var station in engine.Stations)
        {
            sb.AppendLine($"visionstudio_fps{{station=\"{station.Config.StationId}\"}} {station.Statistics.Fps:F1}");
        }
        sb.AppendLine();

        sb.AppendLine("# HELP visionstudio_yield_percentage Cumulative yield percentage (0-100%)");
        sb.AppendLine("# TYPE visionstudio_yield_percentage gauge");
        foreach (var station in engine.Stations)
        {
            sb.AppendLine($"visionstudio_yield_percentage{{station=\"{station.Config.StationId}\"}} {station.Statistics.YieldPercentage:F2}");
        }
        sb.AppendLine();

        return sb.ToString();
    }
}
