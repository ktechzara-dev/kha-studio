using System.IO;
using VisionStudio.Core.Flow;
using VisionStudio.Core.Macro;
using VisionStudio.Core.Pipeline;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Core.Tests.Macro;

public sealed class MacroToolTests
{
    private readonly ToolRegistry _registry;

    public MacroToolTests()
    {
        _registry = new ToolRegistry();
        _registry.RegisterTool<ConstantSourceTool>();
        _registry.RegisterTool<ExpressionTool>();
        _registry.RegisterTool<JudgeTool>();
    }

    [Fact]
    public void MacroTool_ExecutesSubPipeline_And_ProducesMappedOutputs()
    {
        // Construct an inner sub-pipeline:
        // Input: "NumberIn" -> Expr.PortA, Const100 -> Expr.PortB (adds 100) -> Output: "ResultNumber"
        var innerGraph = new PipelineGraph();
        var constNode = new PipelineNode("Const1", new ConstantSourceTool());
        constNode.Tool.Parameters.First(p => p.Name == ConstantSourceTool.ParamNumber).Value = 100.0;

        var exprNode = new PipelineNode("Expr1", new ExpressionTool());
        exprNode.Tool.Parameters.First(p => p.Name == ExpressionTool.ParamOperation).Value = "Add";

        innerGraph.AddNode(constNode);
        innerGraph.AddNode(exprNode);
        innerGraph.AddConnection(new PipelineConnection("Const1", ConstantSourceTool.PortNumber, "Expr1", ExpressionTool.PortB));

        var serializer = new PipelineSerializer();
        var subDoc = serializer.SerializeToDocument(innerGraph);

        var macroDef = new MacroDefinition
        {
            Id = "Add100Macro",
            Name = "Add 100 Macro",
            SerializedPipeline = subDoc
        };

        macroDef.ExposedInputs.Add(new MacroPortMapping
        {
            ExposedPortId = "NumberIn",
            DisplayName = "Number In",
            Type = PortType.Number,
            Direction = PortDirection.Input,
            InternalNodeId = "Expr1",
            InternalPortId = ExpressionTool.PortA
        });

        macroDef.ExposedOutputs.Add(new MacroPortMapping
        {
            ExposedPortId = "ResultNumber",
            DisplayName = "Result Number",
            Type = PortType.Number,
            Direction = PortDirection.Output,
            InternalNodeId = "Expr1",
            InternalPortId = ExpressionTool.PortResultNumber
        });

        var macroTool = new MacroTool(macroDef, _registry);
        var inputs = new Dictionary<string, object?> { ["NumberIn"] = 42.0 };
        var ctx = new ToolContext(inputs);

        var result = macroTool.Execute(ctx, CancellationToken.None);

        result.Status.Should().Be(ToolStatus.Ok);
        result.Outputs["ResultNumber"].Should().Be(142.0);
    }

    [Fact]
    public void NestedMacro_ExecutesRecursively_UpToAnyDepth()
    {
        // Level 1 Macro: adds 10
        var g1 = new PipelineGraph();
        var const10 = new PipelineNode("Const10", new ConstantSourceTool());
        const10.Tool.Parameters.First(p => p.Name == ConstantSourceTool.ParamNumber).Value = 10.0;

        var n1 = new PipelineNode("N1", new ExpressionTool());
        n1.Tool.Parameters.First(p => p.Name == ExpressionTool.ParamOperation).Value = "Add";

        g1.AddNode(const10);
        g1.AddNode(n1);
        g1.AddConnection(new PipelineConnection("Const10", ConstantSourceTool.PortNumber, "N1", ExpressionTool.PortB));

        var ser = new PipelineSerializer();
        var def1 = new MacroDefinition
        {
            Id = "SubMacroAdd10",
            Name = "Sub Macro +10",
            SerializedPipeline = ser.SerializeToDocument(g1)
        };
        def1.ExposedInputs.Add(new MacroPortMapping
        {
            ExposedPortId = "In",
            Type = PortType.Number,
            InternalNodeId = "N1",
            InternalPortId = ExpressionTool.PortA
        });
        def1.ExposedOutputs.Add(new MacroPortMapping
        {
            ExposedPortId = "Out",
            Type = PortType.Number,
            InternalNodeId = "N1",
            InternalPortId = ExpressionTool.PortResultNumber
        });

        // Register SubMacro into registry so parent can instantiate it
        _registry.RegisterFactory($"Macro.{def1.Id}", def1.Name, "Macros", "", () => new MacroTool(def1, _registry));

        // Level 2 (Parent) Macro: contains SubMacro + another node adding 5
        var g2 = new PipelineGraph();
        var childMacroNode = new PipelineNode("ChildNode", new MacroTool(def1, _registry));
        var const5 = new PipelineNode("Const5", new ConstantSourceTool());
        const5.Tool.Parameters.First(p => p.Name == ConstantSourceTool.ParamNumber).Value = 5.0;

        var add5Node = new PipelineNode("Add5Node", new ExpressionTool());
        add5Node.Tool.Parameters.First(p => p.Name == ExpressionTool.ParamOperation).Value = "Add";

        g2.AddNode(childMacroNode);
        g2.AddNode(const5);
        g2.AddNode(add5Node);
        g2.AddConnection(new PipelineConnection("ChildNode", "Out", "Add5Node", ExpressionTool.PortA));
        g2.AddConnection(new PipelineConnection("Const5", ConstantSourceTool.PortNumber, "Add5Node", ExpressionTool.PortB));

        var def2 = new MacroDefinition
        {
            Id = "ParentMacro",
            Name = "Parent Macro (+10 then +5)",
            SerializedPipeline = ser.SerializeToDocument(g2)
        };
        def2.ExposedInputs.Add(new MacroPortMapping
        {
            ExposedPortId = "ParentIn",
            Type = PortType.Number,
            InternalNodeId = "ChildNode",
            InternalPortId = "In"
        });
        def2.ExposedOutputs.Add(new MacroPortMapping
        {
            ExposedPortId = "ParentOut",
            Type = PortType.Number,
            InternalNodeId = "Add5Node",
            InternalPortId = ExpressionTool.PortResultNumber
        });

        var parentMacroTool = new MacroTool(def2, _registry);
        var ctx = new ToolContext(new Dictionary<string, object?> { ["ParentIn"] = 100.0 });

        var result = parentMacroTool.Execute(ctx, CancellationToken.None);

        result.Status.Should().Be(ToolStatus.Ok);
        // 100 + 10 (from child macro) + 5 (from parent node) = 115
        result.Outputs["ParentOut"].Should().Be(115.0);
    }

    [Fact]
    public void MacroFile_SaveAndLoad_RoundtripsSuccessfully()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_macro_{Guid.NewGuid():N}.vsmacro");
        try
        {
            var def = new MacroDefinition
            {
                Id = "TestSaveLoad",
                Name = "Save/Load Test",
                Version = "2.1.0",
                Author = "Antigravity Engineer"
            };
            def.ExposedInputs.Add(new MacroPortMapping { ExposedPortId = "In1", Type = PortType.Number });
            def.ExposedOutputs.Add(new MacroPortMapping { ExposedPortId = "Out1", Type = PortType.Boolean });

            def.SaveToFile(tempFile);
            File.Exists(tempFile).Should().BeTrue();

            var loaded = MacroDefinition.LoadFromFile(tempFile);
            loaded.Id.Should().Be("TestSaveLoad");
            loaded.Name.Should().Be("Save/Load Test");
            loaded.Version.Should().Be("2.1.0");
            loaded.Author.Should().Be("Antigravity Engineer");
            loaded.ExposedInputs.Should().HaveCount(1);
            loaded.ExposedOutputs.Should().HaveCount(1);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
