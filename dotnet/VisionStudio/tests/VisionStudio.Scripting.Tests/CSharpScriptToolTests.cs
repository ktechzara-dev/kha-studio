using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;
using VisionStudio.Scripting.Host;

namespace VisionStudio.Scripting.Tests;

public sealed class CSharpScriptToolTests
{
    [Fact]
    public void CSharpScriptTool_ExecutesScriptInPipelineContext()
    {
        var tool = new CSharpScriptTool();
        tool.Parameters.First(p => p.Name == CSharpScriptTool.ParamScript).Value = @"
            var valA = GetNumber(""InputA"");
            var valB = GetNumber(""InputB"");
            Set(""OutputA"", valA * 2 + valB);
            Set(""Passed"", valA > 10);
        ";

        var inputs = new Dictionary<string, object?>
        {
            [CSharpScriptTool.PortInA] = 25.0,
            [CSharpScriptTool.PortInB] = 7.0
        };

        var ctx = new ToolContext(inputs);
        var result = tool.Execute(ctx, CancellationToken.None);

        result.Status.Should().Be(ToolStatus.Ok);
        result.Outputs[CSharpScriptTool.PortOutA].Should().Be(57.0); // 25*2 + 7 = 57
        result.Outputs[CSharpScriptTool.PortOutPassed].Should().Be(true);
    }
}
