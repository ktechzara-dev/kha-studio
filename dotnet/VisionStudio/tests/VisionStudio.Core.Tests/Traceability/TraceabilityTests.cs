using System.Text.Json;
using FluentAssertions;
using VisionStudio.Core.Traceability;
using Xunit;

namespace VisionStudio.Core.Tests.Traceability;

public class TraceabilityTests : IDisposable
{
    private readonly string _testDir;

    public TraceabilityTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "VS_TraceTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir)) Directory.Delete(_testDir, true);
        }
        catch { }
    }

    [Fact]
    public void TraceabilityStore_AppendsRecords_AndMaintainsValidHashChain()
    {
        var store = new TraceabilityStore(_testDir);

        var r1 = store.AppendRecord(new InspectionRecord
        {
            LotNumber = "LOT-2026-A",
            SerialNumber = "SN-001",
            Verdict = InspectionVerdict.Pass,
            ExecutionTimeMs = 12.5,
            Measurements = new() { ["Diameter"] = 25.4, ["Roundness"] = 0.98 }
        });

        var r2 = store.AppendRecord(new InspectionRecord
        {
            LotNumber = "LOT-2026-A",
            SerialNumber = "SN-002",
            Verdict = InspectionVerdict.Fail,
            ExecutionTimeMs = 14.1,
            Measurements = new() { ["Diameter"] = 28.1, ["Roundness"] = 0.65 },
            FailureReasons = new() { "Diameter out of tolerance" }
        });

        r1.SequenceNumber.Should().Be(1);
        r1.PreviousRecordHash.Should().Be("GENESIS");
        r1.RecordHash.Should().NotBeNullOrEmpty();

        r2.SequenceNumber.Should().Be(2);
        r2.PreviousRecordHash.Should().Be(r1.RecordHash);
        r2.RecordHash.Should().NotBeNullOrEmpty();

        var validation = store.ValidateHashChain();
        validation.IsValid.Should().BeTrue();
        validation.TotalRecords.Should().Be(2);
    }

    [Fact]
    public void TraceabilityStore_DetectsDataTampering_WhenFileIsModified()
    {
        var store = new TraceabilityStore(_testDir);

        store.AppendRecord(new InspectionRecord
        {
            LotNumber = "LOT-01",
            SerialNumber = "SN-100",
            Verdict = InspectionVerdict.Fail // Original is Fail
        });

        store.AppendRecord(new InspectionRecord
        {
            LotNumber = "LOT-01",
            SerialNumber = "SN-101",
            Verdict = InspectionVerdict.Pass
        });

        // Verify clean state
        store.ValidateHashChain().IsValid.Should().BeTrue();

        // TAMPER: Malicious actor modifies the JSONL file to fraudulently change Verdict to Pass!
        string filePath = Path.Combine(_testDir, "inspections.jsonl");
        string content = File.ReadAllText(filePath);
        // Change Verdict from 1 (Fail) to 0 (Pass) in record 1
        string tamperedContent = content.Replace("\"Verdict\":1", "\"Verdict\":0");
        File.WriteAllText(filePath, tamperedContent);

        // Reload store
        var reloadedStore = new TraceabilityStore(_testDir);
        var validation = reloadedStore.ValidateHashChain();

        validation.IsValid.Should().BeFalse("Tampering must be cryptographically detected");
        validation.TamperedIndex.Should().Be(0);
        validation.Message.Should().Contain("mismatch");
    }

    [Fact]
    public void TraceabilityStore_QueryFilters_WorkAccurately()
    {
        var store = new TraceabilityStore(_testDir);

        store.AppendRecord(new InspectionRecord { LotNumber = "LOT-A", SerialNumber = "SN-1", Verdict = InspectionVerdict.Pass });
        store.AppendRecord(new InspectionRecord { LotNumber = "LOT-A", SerialNumber = "SN-2", Verdict = InspectionVerdict.Fail });
        store.AppendRecord(new InspectionRecord { LotNumber = "LOT-B", SerialNumber = "SN-3", Verdict = InspectionVerdict.Pass });

        var passOnly = store.QueryRecords(new TraceFilter { Verdict = InspectionVerdict.Pass });
        passOnly.Should().HaveCount(2);

        var lotAOnly = store.QueryRecords(new TraceFilter { LotNumber = "LOT-A" });
        lotAOnly.Should().HaveCount(2);

        var sn2Only = store.QueryRecords(new TraceFilter { SerialNumber = "SN-2" });
        sn2Only.Should().HaveCount(1);
        sn2Only[0].Verdict.Should().Be(InspectionVerdict.Fail);
    }

    [Fact]
    public void TraceabilityStore_ExportToCsv_CreatesValidFile()
    {
        var store = new TraceabilityStore(_testDir);
        store.AppendRecord(new InspectionRecord
        {
            LotNumber = "LOT-99",
            SerialNumber = "SN-999",
            Verdict = InspectionVerdict.Pass,
            ExecutionTimeMs = 15.3
        });

        string csvPath = Path.Combine(_testDir, "export.csv");
        store.ExportToCsv(csvPath, store.Records);

        File.Exists(csvPath).Should().BeTrue();
        string csvText = File.ReadAllText(csvPath);
        csvText.Should().Contain("SequenceNumber,Id,TimestampUtc");
        csvText.Should().Contain("SN-999");
        csvText.Should().Contain("LOT-99");
    }

    [Fact]
    public void AuditTrail_LogsActions_AndMaintainsIntegrity()
    {
        string auditPath = Path.Combine(_testDir, "audit.jsonl");
        var audit = new AuditTrail(auditPath);

        audit.LogAction("SwitchRecipe", "RecipeBC-500", "Switched from BC-250", user: "Tech1");
        audit.LogAction("PromoteModel", "PatchCore_v2", "Promoted to production", user: "Admin");

        audit.Events.Should().HaveCount(2);
        audit.VerifyIntegrity().Should().BeTrue();
    }
}
