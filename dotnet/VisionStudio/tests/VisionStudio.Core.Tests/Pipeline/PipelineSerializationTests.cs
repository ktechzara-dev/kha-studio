using VisionStudio.Core.Flow;
using VisionStudio.Core.Overlays;
using VisionStudio.Core.Pipeline;
using VisionStudio.Core.Tools;

namespace VisionStudio.Core.Tests.Pipeline;

public class PipelineSerializationTests
{
    [Fact]
    public async Task Build_Serialize_Deserialize_And_Execute_3ToolPipeline_Succeeds()
    {
        // 1. Setup registry
        var registry = new ToolRegistry();
        registry.RegisterAssembly(typeof(ConstantSourceTool).Assembly);

        // 2. Build original pipeline
        var originalGraph = new PipelineGraph();

        // Node 1: Source emitting 25.0
        var sourceTool = (ConstantSourceTool)registry.CreateInstance("Flow.ConstantSource");
        sourceTool.Parameters.First(p => p.Name == ConstantSourceTool.ParamNumber).Value = 25.0;
        var nodeSource = new PipelineNode("Node1_Source", sourceTool, "Measurement Source", 100, 200);

        // Node 2: Expression multiplying A by 2.0 (via input port default or param)
        var exprTool = (ExpressionTool)registry.CreateInstance("Flow.Expression");
        exprTool.Parameters.First(p => p.Name == ExpressionTool.ParamOperation).Value = "Multiply";
        var nodeExpr = new PipelineNode("Node2_Expr", exprTool, "Scale Multiplier", 350, 200);

        // We can create a second constant source for multiplier B = 2.0, or use constant B
        var const2Tool = (ConstantSourceTool)registry.CreateInstance("Flow.ConstantSource");
        const2Tool.Parameters.First(p => p.Name == ConstantSourceTool.ParamNumber).Value = 2.0;
        var nodeMultiplier = new PipelineNode("Node2_B", const2Tool, "Multiplier Constant", 100, 350);

        // Node 3: Judge with limits [40.0, 60.0]
        var judgeTool = (JudgeTool)registry.CreateInstance("Output.Judge");
        judgeTool.Parameters.First(p => p.Name == JudgeTool.ParamLower).Value = 40.0;
        judgeTool.Parameters.First(p => p.Name == JudgeTool.ParamUpper).Value = 60.0;
        var nodeJudge = new PipelineNode("Node3_Judge", judgeTool, "Pass/Fail Verdict", 600, 200);

        originalGraph.AddNode(nodeSource);
        originalGraph.AddNode(nodeMultiplier);
        originalGraph.AddNode(nodeExpr);
        originalGraph.AddNode(nodeJudge);

        // Connect Node1 (25.0) -> Expr.A
        originalGraph.AddConnection(new PipelineConnection("Node1_Source", ConstantSourceTool.PortNumber, "Node2_Expr", ExpressionTool.PortA));
        // Connect Node2_B (2.0) -> Expr.B
        originalGraph.AddConnection(new PipelineConnection("Node2_B", ConstantSourceTool.PortNumber, "Node2_Expr", ExpressionTool.PortB));
        // Connect Expr (25.0 * 2.0 = 50.0) -> Judge.Value
        originalGraph.AddConnection(new PipelineConnection("Node2_Expr", ExpressionTool.PortResultNumber, "Node3_Judge", JudgeTool.PortValue));

        // 3. Serialize to JSON
        var serializer = new PipelineSerializer();
        var json = serializer.SerializeToJson(originalGraph, "Industrial 3-Tool Inspection Pipeline");

        json.Should().NotBeNullOrWhiteSpace();
        json.Should().Contain("\"schemaVersion\": \"1.0.0\"");
        json.Should().Contain("Node1_Source");
        json.Should().Contain("Node2_Expr");
        json.Should().Contain("Node3_Judge");

        // 4. Deserialize from JSON into a completely new PipelineGraph
        var restoredGraph = serializer.DeserializeFromJson(json, registry);

        restoredGraph.Nodes.Should().HaveCount(4);
        restoredGraph.Connections.Should().HaveCount(3);
        restoredGraph.Validate().IsValid.Should().BeTrue();

        // Verify deserialized parameters were restored accurately
        var restoredSource = (ConstantSourceTool)restoredGraph.GetNode("Node1_Source")!.Tool;
        restoredSource.Parameters.First(p => p.Name == ConstantSourceTool.ParamNumber).GetValue<double>().Should().Be(25.0);

        // 5. Execute restored pipeline with PipelineExecutor
        var executor = new PipelineExecutor();
        var execResult = await executor.ExecuteAsync(restoredGraph);

        // 6. Assert end-to-end execution results
        execResult.Success.Should().BeTrue();
        execResult.ErrorMessage.Should().BeNull();

        // Check Expr Result
        var exprResult = execResult.NodeResults["Node2_Expr"];
        exprResult.Status.Should().Be(ToolStatus.Ok);
        var calculatedNumber = exprResult.GetOutput<double>(ExpressionTool.PortResultNumber);
        calculatedNumber.Should().Be(50.0); // 25.0 * 2.0

        // Check Judge Verdict
        var judgeResult = execResult.NodeResults["Node3_Judge"];
        judgeResult.Status.Should().Be(ToolStatus.Ok);
        var isOk = judgeResult.GetOutput<bool>(JudgeTool.PortIsOk);
        var verdict = judgeResult.GetOutput<string>(JudgeTool.PortVerdict);

        isOk.Should().BeTrue();
        verdict.Should().Be("OK");

        // Check Overlays
        judgeResult.Overlays.Should().ContainSingle();
        var textOverlay = judgeResult.Overlays.First() as TextOverlay;
        textOverlay.Should().NotBeNull();
        textOverlay!.Text.Should().Be("OK");
    }
}
