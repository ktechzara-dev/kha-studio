using OpenCvSharp;
using VisionStudio.Cli;
using VisionStudio.Core.Recipes;
using VisionStudio.Core.Testing;
using VisionStudio.Core.Traceability;

namespace VisionStudio.Cli.Tests;

public class CliCommandTests : IDisposable
{
    private readonly string _testDir;

    public CliCommandTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "VS_CliTests_" + Guid.NewGuid().ToString("N"));
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
    public async Task Cli_HelpAndVersion_ReturnZero()
    {
        int helpCode = await Program.Main(new[] { "--help" });
        helpCode.Should().Be(0);

        int verCode = await Program.Main(new[] { "--version" });
        verCode.Should().Be(0);
    }

    [Fact]
    public async Task Cli_VerifyTrace_ReturnsZeroForValidChain_AndOneForTampered()
    {
        string storeDir = Path.Combine(_testDir, "trace_store");
        var store = new TraceabilityStore(storeDir);

        store.AppendRecord(new InspectionRecord { SerialNumber = "SN-1", Verdict = InspectionVerdict.Pass });
        store.AppendRecord(new InspectionRecord { SerialNumber = "SN-2", Verdict = InspectionVerdict.Pass });

        // 1. Verify valid store
        int codeClean = await Program.Main(new[] { "verify-trace", "--store", storeDir });
        codeClean.Should().Be(0);

        // 2. Tamper store file
        string logPath = Path.Combine(storeDir, "inspections.jsonl");
        string content = File.ReadAllText(logPath);
        string tampered = content.Replace("SN-1", "SN-HACKED");
        File.WriteAllText(logPath, tampered);

        // 3. Verify detects tamper
        int codeTampered = await Program.Main(new[] { "verify-trace", "--store", storeDir });
        codeTampered.Should().Be(1);
    }

    [Fact]
    public async Task Cli_RecipeDiff_ReturnsZeroAndPrintsDifferences()
    {
        string r1Path = Path.Combine(_testDir, "r1.vsrecipe");
        string r2Path = Path.Combine(_testDir, "r2.vsrecipe");

        var r1 = new Recipe { Name = "BatchA" };
        r1.SetParameter("Tool1", "ParamA", 10.0);
        r1.Save(r1Path);

        var r2 = new Recipe { Name = "BatchB" };
        r2.SetParameter("Tool1", "ParamA", 20.0);
        r2.Save(r2Path);

        int code = await Program.Main(new[] { "recipe", "diff", "--r1", r1Path, "--r2", r2Path });
        code.Should().Be(0);
    }

    [Fact]
    public async Task Cli_TestCommand_ExecutesTestSetAndReturnsZeroOnPass()
    {
        string imgPath = Path.Combine(_testDir, "sample.png");
        using (var mat = new Mat(32, 32, MatType.CV_8UC1, new Scalar(128)))
        {
            Cv2.ImWrite(imgPath, mat);
        }

        string setPath = Path.Combine(_testDir, "testset.vstest");
        var set = new TestSet
        {
            Name = "CLI Verification Suite",
            Items = new List<TestSetItem>
            {
                new()
                {
                    ImagePath = imgPath,
                    ExpectedVerdict = InspectionVerdict.Pass
                }
            }
        };
        set.Save(setPath);

        int code = await Program.Main(new[] { "test", "--project", "mock.vspkg", "--set", setPath });
        code.Should().Be(0);
    }
}
