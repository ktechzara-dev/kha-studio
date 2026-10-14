using VisionStudio.Scripting.Host;

namespace VisionStudio.Scripting.Tests;

public sealed class ScriptHostTests
{
    [Fact]
    public async Task ExecuteAsync_ExecutesArithmeticAndSetsOutputs()
    {
        var host = new ScriptHost();
        var inputs = new Dictionary<string, object?>
        {
            ["Width"] = 120.0,
            ["Height"] = 80.0
        };

        var ctx = new ScriptContext(inputs);
        string code = @"
            var w = GetNumber(""Width"");
            var h = GetNumber(""Height"");
            var area = w * h;
            Set(""Area"", area);
            Set(""IsLarge"", area > 5000);
        ";

        var result = await host.ExecuteAsync(code, ctx);

        result.Success.Should().BeTrue();
        ctx.Outputs["Area"].Should().Be(9600.0);
        ctx.Outputs["IsLarge"].Should().Be(true);
    }

    [Fact]
    public async Task ExecuteAsync_BlocksDangerousNamespaces_InSandbox()
    {
        var host = new ScriptHost();
        var ctx = new ScriptContext(new Dictionary<string, object?>());

        string maliciousCode = @"
            System.IO.File.Delete(""C:\\test.txt"");
        ";

        var result = await host.ExecuteAsync(maliciousCode, ctx);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("System.IO.File"));
    }

    [Fact]
    public async Task ExecuteAsync_EnforcesTimeout_OnInfiniteLoops()
    {
        var host = new ScriptHost();
        var ctx = new ScriptContext(new Dictionary<string, object?>());

        string infiniteCode = @"
            while (true) { }
        ";

        // Set a short timeout of 200 ms
        var result = await host.ExecuteAsync(infiniteCode, ctx, timeoutMs: 200);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("quá thời gian"));
    }

    [Fact]
    public void Compile_ReturnsCompilerDiagnostics_OnSyntaxErrors()
    {
        var host = new ScriptHost();
        string brokenCode = "int x = ;"; // syntax error

        var (success, diagnostics) = host.Compile(brokenCode);

        success.Should().BeFalse();
        diagnostics.Should().NotBeEmpty();
    }
}
