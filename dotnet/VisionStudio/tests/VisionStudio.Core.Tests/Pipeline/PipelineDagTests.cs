using VisionStudio.Core.Flow;
using VisionStudio.Core.Pipeline;

namespace VisionStudio.Core.Tests.Pipeline;

public class PipelineDagTests
{
    [Fact]
    public void CycleDetection_DetectsCycleInConnectedNodes()
    {
        var graph = new PipelineGraph();
        var nodeA = new PipelineNode("A", new ConstantSourceTool());
        var nodeB = new PipelineNode("B", new ExpressionTool());
        var nodeC = new PipelineNode("C", new JudgeTool());

        graph.AddNode(nodeA);
        graph.AddNode(nodeB);
        graph.AddNode(nodeC);

        graph.AddConnection(new PipelineConnection("A", ConstantSourceTool.PortNumber, "B", ExpressionTool.PortA));
        graph.AddConnection(new PipelineConnection("B", ExpressionTool.PortResultNumber, "C", JudgeTool.PortValue));

        graph.HasCycle().Should().BeFalse();
        graph.Validate().IsValid.Should().BeTrue();

        // Introduce circular feedback: C -> B
        graph.AddConnection(new PipelineConnection("C", JudgeTool.PortIsOk, "B", ExpressionTool.PortB));
        graph.HasCycle().Should().BeTrue();
        graph.Validate().IsValid.Should().BeFalse();
    }

    [Fact]
    public void TopologicalSort_OrdersNodesByDependency()
    {
        var graph = new PipelineGraph();
        var nodeA = new PipelineNode("NodeA", new ConstantSourceTool());
        var nodeB = new PipelineNode("NodeB", new ExpressionTool());
        var nodeC = new PipelineNode("NodeC", new JudgeTool());

        // Add in mixed order
        graph.AddNode(nodeC);
        graph.AddNode(nodeA);
        graph.AddNode(nodeB);

        graph.AddConnection(new PipelineConnection("NodeA", ConstantSourceTool.PortNumber, "NodeB", ExpressionTool.PortA));
        graph.AddConnection(new PipelineConnection("NodeB", ExpressionTool.PortResultNumber, "NodeC", JudgeTool.PortValue));

        var order = graph.GetTopologicalOrder();
        order.Select(n => n.Id).Should().ContainInOrder("NodeA", "NodeB", "NodeC");
    }

    [Fact]
    public void ParameterChange_InvalidatesDownstreamNodesOnly()
    {
        var graph = new PipelineGraph();
        var src = new PipelineNode("Source", new ConstantSourceTool());
        var expr = new PipelineNode("Expr", new ExpressionTool());
        var judge = new PipelineNode("Judge", new JudgeTool());

        graph.AddNode(src);
        graph.AddNode(expr);
        graph.AddNode(judge);

        graph.AddConnection(new PipelineConnection("Source", ConstantSourceTool.PortNumber, "Expr", ExpressionTool.PortA));
        graph.AddConnection(new PipelineConnection("Expr", ExpressionTool.PortResultNumber, "Judge", JudgeTool.PortValue));

        // Mark all clean initially
        src.IsDirty = false;
        expr.IsDirty = false;
        judge.IsDirty = false;

        // Act: modify parameter on Expr
        var param = expr.Tool.Parameters.First(p => p.Name == ExpressionTool.ParamOperation);
        param.Value = "Multiply";

        // Assert: Expr and downstream Judge should be dirty, but upstream Source remains clean
        src.IsDirty.Should().BeFalse();
        expr.IsDirty.Should().BeTrue();
        judge.IsDirty.Should().BeTrue();
    }
}
