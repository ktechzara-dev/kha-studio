using VisionStudio.Core.Traceability;

namespace VisionStudio.Runtime.Stations;

public enum StationTriggerMode
{
    Software = 0,
    Continuous = 1,
    Hardware = 2
}

public sealed class StationConfig
{
    public string StationId { get; set; } = "Station_01";
    public string Name { get; set; } = "Surface Inspection Station";
    public string PipelineFile { get; set; } = "pipeline.json";
    public string? DefaultRecipe { get; set; }
    public bool Enabled { get; set; } = true;
    public StationTriggerMode TriggerMode { get; set; } = StationTriggerMode.Software;
    public int ContinuousIntervalMs { get; set; } = 500;
}

public sealed class StationStatistics
{
    public long TotalCount { get; set; }
    public long PassCount { get; set; }
    public long FailCount { get; set; }
    public double YieldPercentage => TotalCount == 0 ? 100.0 : Math.Round((double)PassCount / TotalCount * 100.0, 2);
    public double LastCycleTimeMs { get; set; }
    public double AverageCycleTimeMs { get; set; }
    public double Fps => LastCycleTimeMs > 0 ? Math.Round(1000.0 / LastCycleTimeMs, 1) : 0.0;
    public InspectionVerdict? LastVerdict { get; set; }
    public DateTimeOffset? LastInspectionTime { get; set; }
    public string? LastInspectionId { get; set; }

    private readonly Queue<double> _recentCycleTimes = new();

    public void RecordExecution(InspectionRecord record)
    {
        TotalCount++;
        if (record.Verdict == InspectionVerdict.Pass)
        {
            PassCount++;
        }
        else
        {
            FailCount++;
        }

        LastVerdict = record.Verdict;
        LastInspectionTime = record.Timestamp;
        LastInspectionId = record.Id;
        LastCycleTimeMs = record.ExecutionTimeMs;

        lock (_recentCycleTimes)
        {
            _recentCycleTimes.Enqueue(record.ExecutionTimeMs);
            if (_recentCycleTimes.Count > 50)
            {
                _recentCycleTimes.Dequeue();
            }
            AverageCycleTimeMs = Math.Round(_recentCycleTimes.Average(), 2);
        }
    }

    public void Reset()
    {
        TotalCount = 0;
        PassCount = 0;
        FailCount = 0;
        LastCycleTimeMs = 0;
        AverageCycleTimeMs = 0;
        LastVerdict = null;
        LastInspectionTime = null;
        LastInspectionId = null;
        lock (_recentCycleTimes)
        {
            _recentCycleTimes.Clear();
        }
    }
}
