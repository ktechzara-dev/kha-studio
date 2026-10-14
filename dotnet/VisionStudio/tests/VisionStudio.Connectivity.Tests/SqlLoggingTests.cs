using FluentAssertions;
using VisionStudio.Connectivity.Sql;
using VisionStudio.Core.Traceability;
using Xunit;

namespace VisionStudio.Connectivity.Tests;

public class SqlLoggingTests : IDisposable
{
    private readonly string _dbPath;

    public SqlLoggingTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), "VS_SqlTests_" + Guid.NewGuid().ToString("N"), "inspections.db");
    }

    public void Dispose()
    {
        try
        {
            var dir = Path.GetDirectoryName(_dbPath);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
        catch { }
    }

    [Fact]
    public async Task SqlInspectionLogger_LogsRecords_AndQueriesAccurately()
    {
        await using var logger = new SqlInspectionLogger(_dbPath);

        var r1 = new InspectionRecord
        {
            SequenceNumber = 1,
            LotNumber = "BATCH-A",
            SerialNumber = "SN-001",
            Verdict = InspectionVerdict.Pass,
            ExecutionTimeMs = 10.5,
            Measurements = new() { ["Area"] = 500.0 }
        };

        var r2 = new InspectionRecord
        {
            SequenceNumber = 2,
            LotNumber = "BATCH-A",
            SerialNumber = "SN-002",
            Verdict = InspectionVerdict.Fail,
            ExecutionTimeMs = 12.0,
            Measurements = new() { ["Area"] = 350.0 }
        };

        await logger.LogBatchAsync(new[] { r1, r2 });

        // Query all
        var all = await logger.QueryAsync(new SqlTraceFilter());
        all.Should().HaveCount(2);

        // Query by verdict
        var fails = await logger.QueryAsync(new SqlTraceFilter { Verdict = InspectionVerdict.Fail });
        fails.Should().HaveCount(1);
        fails[0].SerialNumber.Should().Be("SN-002");
        fails[0].Measurements.Should().ContainKey("Area");
        fails[0].Measurements["Area"].Should().Be(350.0);
    }
}
