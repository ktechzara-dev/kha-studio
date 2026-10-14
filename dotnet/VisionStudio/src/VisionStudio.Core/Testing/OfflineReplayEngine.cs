using System.Diagnostics;
using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Pipeline;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;
using VisionStudio.Core.Traceability;

namespace VisionStudio.Core.Testing;

/// <summary>
/// Executes offline regression testing and historical replay against pipeline graphs.
/// Verifies golden test sets for CI/CD gates and evaluates measurement drift across versions.
/// </summary>
public sealed class OfflineReplayEngine
{
    /// <summary>
    /// Executes a golden test set through the pipeline graph and evaluates pass/fail status.
    /// </summary>
    public async Task<TestSetRunReport> RunTestSetAsync(
        PipelineExecutor executor,
        PipelineGraph graph,
        TestSet testSet,
        IProgress<(int Current, int Total)>? progress = null,
        CancellationToken ct = default)
    {
        var results = new List<TestItemResult>();
        int total = testSet.Items.Count;
        double totalLatency = 0.0;

        for (int i = 0; i < total; i++)
        {
            ct.ThrowIfCancellationRequested();
            var item = testSet.Items[i];
            progress?.Report((i + 1, total));

            if (!File.Exists(item.ImagePath))
            {
                results.Add(new TestItemResult(
                    Item: item,
                    ActualVerdict: InspectionVerdict.Fail,
                    ActualMeasurements: new Dictionary<string, double>(),
                    VerdictMatches: false,
                    TolerancesPassed: false,
                    Success: false,
                    FailureReason: $"Test image file not found: {item.ImagePath}",
                    LatencyMs: 0.0));
                continue;
            }

            var sw = Stopwatch.StartNew();
            using var mat = Cv2.ImRead(item.ImagePath, ImreadModes.Unchanged);
            using var frame = new ImageFrame(mat, cameraId: "ReplaySource");

            // Find entry image ports in graph
            var externalInputs = new Dictionary<(string NodeId, string PortId), object?>();
            foreach (var node in graph.Nodes)
            {
                foreach (var inputPort in node.Tool.Inputs.Where(p => p.Type == PortType.Image))
                {
                    // If port is not connected from upstream, inject the replay image
                    bool hasIncoming = graph.Connections.Any(c =>
                        c.TargetNodeId.Equals(node.Id, StringComparison.OrdinalIgnoreCase) &&
                        c.TargetPortId.Equals(inputPort.Id, StringComparison.OrdinalIgnoreCase));

                    if (!hasIncoming)
                    {
                        externalInputs[(node.Id, inputPort.Id)] = frame;
                    }
                }
            }

            // Invalidate graph nodes to ensure fresh execution
            foreach (var n in graph.Nodes) n.MarkDirty();

            var execResult = await executor.ExecuteAsync(graph, stopAtBreakpoints: false, ct, externalInputs);
            sw.Stop();
            double latency = sw.Elapsed.TotalMilliseconds;
            totalLatency += latency;

            // Extract verdict and measurements from execution results
            var (actualVerdict, measurements, errorReason) = EvaluateExecutionOutcome(execResult);

            bool verdictMatch = (actualVerdict == item.ExpectedVerdict);
            bool tolerancesPassed = true;
            string? failureReason = null;

            if (!verdictMatch)
            {
                failureReason = $"Verdict mismatch: expected {item.ExpectedVerdict}, got {actualVerdict}.";
            }

            // Verify tolerances
            foreach (var (key, tol) in item.ExpectedMeasurements)
            {
                if (measurements.TryGetValue(key, out double val))
                {
                    if (!tol.IsInRange(val))
                    {
                        tolerancesPassed = false;
                        failureReason = (failureReason == null ? "" : failureReason + " ") +
                                        $"Measurement '{key}' value {val:F3} outside tolerance [{tol.LowerLimit:F3}, {tol.UpperLimit:F3}].";
                    }
                }
                else
                {
                    tolerancesPassed = false;
                    failureReason = (failureReason == null ? "" : failureReason + " ") +
                                    $"Expected measurement '{key}' missing from outputs.";
                }
            }

            bool isSuccess = verdictMatch && tolerancesPassed && execResult.Success;
            if (!execResult.Success && failureReason == null)
            {
                failureReason = execResult.ErrorMessage ?? "Pipeline execution failed.";
            }

            results.Add(new TestItemResult(
                Item: item,
                ActualVerdict: actualVerdict,
                ActualMeasurements: measurements,
                VerdictMatches: verdictMatch,
                TolerancesPassed: tolerancesPassed,
                Success: isSuccess,
                FailureReason: failureReason,
                LatencyMs: latency));
        }

        return new TestSetRunReport
        {
            TestSetName = testSet.Name,
            TotalItems = total,
            PassedItems = results.Count(r => r.Success),
            FailedItems = results.Count(r => !r.Success),
            AverageLatencyMs = total > 0 ? totalLatency / total : 0.0,
            Details = results
        };
    }

    /// <summary>
    /// Replays a batch of historical inspection records through the pipeline to detect verdict regressions or measurement drifts.
    /// </summary>
    public async Task<ReplayRegressionReport> ReplayTraceBatchAsync(
        PipelineExecutor executor,
        PipelineGraph graph,
        IEnumerable<InspectionRecord> records,
        IProgress<(int Current, int Total)>? progress = null,
        CancellationToken ct = default)
    {
        var recordList = records.ToList();
        int total = recordList.Count;
        var rows = new List<ReplayRegressionRow>();

        for (int i = 0; i < total; i++)
        {
            ct.ThrowIfCancellationRequested();
            var r = recordList[i];
            progress?.Report((i + 1, total));

            if (!File.Exists(r.ImagePath)) continue;

            using var mat = Cv2.ImRead(r.ImagePath, ImreadModes.Unchanged);
            using var frame = new ImageFrame(mat, cameraId: "ReplaySource");

            var externalInputs = new Dictionary<(string NodeId, string PortId), object?>();
            foreach (var node in graph.Nodes)
            {
                foreach (var inputPort in node.Tool.Inputs.Where(p => p.Type == PortType.Image))
                {
                    bool hasIncoming = graph.Connections.Any(c =>
                        c.TargetNodeId.Equals(node.Id, StringComparison.OrdinalIgnoreCase) &&
                        c.TargetPortId.Equals(inputPort.Id, StringComparison.OrdinalIgnoreCase));

                    if (!hasIncoming)
                    {
                        externalInputs[(node.Id, inputPort.Id)] = frame;
                    }
                }
            }

            foreach (var n in graph.Nodes) n.MarkDirty();

            var execResult = await executor.ExecuteAsync(graph, stopAtBreakpoints: false, ct, externalInputs);
            var (newVerdict, newMeasurements, _) = EvaluateExecutionOutcome(execResult);

            var drifts = new Dictionary<string, (double HistoricalVal, double NewVal, double Drift)>(StringComparer.OrdinalIgnoreCase);
            foreach (var (k, histVal) in r.Measurements)
            {
                if (newMeasurements.TryGetValue(k, out double newVal))
                {
                    drifts[k] = (histVal, newVal, Math.Round(newVal - histVal, 4));
                }
            }

            bool isMatch = (newVerdict == r.Verdict);
            rows.Add(new ReplayRegressionRow(
                SerialNumber: r.SerialNumber,
                ImagePath: r.ImagePath,
                HistoricalVerdict: r.Verdict,
                NewVerdict: newVerdict,
                IsVerdictMatch: isMatch,
                MeasurementDrifts: drifts));
        }

        return new ReplayRegressionReport
        {
            TotalReplayed = rows.Count,
            MatchingVerdicts = rows.Count(x => x.IsVerdictMatch),
            RegressionsDetected = rows.Count(x => !x.IsVerdictMatch),
            Rows = rows
        };
    }

    private static (InspectionVerdict Verdict, Dictionary<string, double> Measurements, string? Error)
        EvaluateExecutionOutcome(PipelineExecutionResult result)
    {
        var measurements = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        bool hasError = !result.Success;
        bool hasFailedVerdict = false;

        foreach (var (nodeId, toolRes) in result.NodeResults)
        {
            if (toolRes.Status == ToolStatus.Error)
            {
                hasError = true;
            }

            foreach (var (portName, val) in toolRes.Outputs)
            {
                string key = $"{nodeId}.{portName}";
                if (val is double d) measurements[key] = d;
                else if (val is float f) measurements[key] = f;
                else if (val is int n) measurements[key] = n;
                else if (val is long l) measurements[key] = l;
                else if (val is bool b)
                {
                    // Check if a pass/fail flag returned false
                    if (string.Equals(portName, "Passed", StringComparison.OrdinalIgnoreCase) && !b)
                    {
                        hasFailedVerdict = true;
                    }
                }
            }
        }

        InspectionVerdict verdict;
        if (hasError || hasFailedVerdict)
        {
            verdict = InspectionVerdict.Fail;
        }
        else
        {
            verdict = InspectionVerdict.Pass;
        }

        return (verdict, measurements, result.ErrorMessage);
    }
}
