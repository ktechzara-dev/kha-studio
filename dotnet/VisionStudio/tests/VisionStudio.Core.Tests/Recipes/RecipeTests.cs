using FluentAssertions;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Pipeline;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Recipes;
using VisionStudio.Core.Tools;
using Xunit;

namespace VisionStudio.Core.Tests.Recipes;

public sealed class DummyInspectionTool : VisionToolBase
{
    public ToolParameter ParamThreshold { get; }
    public ToolParameter ParamGain { get; }

    public DummyInspectionTool() : base("Dummy.Inspection", "Dummy Inspection", "Test")
    {
        ParamThreshold = AddParameter(new ToolParameter("Threshold", "Threshold", ParameterType.Double, 128.0));
        ParamGain = AddParameter(new ToolParameter("Gain", "Gain", ParameterType.Double, 1.0));

        AddInput(new ToolPort("ImageIn", "Input Image", PortType.Image, PortDirection.Input));
        AddOutput(new ToolPort("Passed", "Passed", PortType.Boolean, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        return ToolResult.Ok(new Dictionary<string, object?> { ["Passed"] = true });
    }
}

public class RecipeTests : IDisposable
{
    private readonly string _tempDir;

    public RecipeTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "VS_RecipeTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
        }
        catch { }
    }

    [Fact]
    public void Recipe_SetAndGetParameters_AndClone()
    {
        var recipe = new Recipe
        {
            Name = "Bottle Cap 500ml",
            ProductCode = "BC-500"
        };

        recipe.SetParameter("Node1", "Threshold", 150.0);
        recipe.SetParameter("Node1", "Gain", 2.5);

        recipe.GetParameter("Node1", "Threshold").Should().Be(150.0);
        recipe.GetParameter("Node1", "Gain").Should().Be(2.5);

        var cloned = recipe.Clone("Bottle Cap 1000ml", "BC-1000");
        cloned.Name.Should().Be("Bottle Cap 1000ml");
        cloned.ProductCode.Should().Be("BC-1000");
        cloned.GetParameter("Node1", "Threshold").Should().Be(150.0);
    }

    [Fact]
    public void Recipe_SerializationRoundtrip()
    {
        var recipe = new Recipe
        {
            Name = "Vial Inspection",
            ProductCode = "VIAL-A"
        };
        recipe.SetParameter("Cam1", "ExposureUs", 5000);
        recipe.Tolerances["Height"] = new MeasurementTolerance("Height", 50.0, 48.0, 52.0);

        string filePath = Path.Combine(_tempDir, "recipe.vsrecipe");
        recipe.Save(filePath);

        var loaded = Recipe.Load(filePath);
        loaded.Name.Should().Be("Vial Inspection");
        loaded.ProductCode.Should().Be("VIAL-A");
        loaded.Tolerances.Should().ContainKey("Height");
        loaded.Tolerances["Height"].IsInRange(51.2).Should().BeTrue();
        loaded.Tolerances["Height"].IsInRange(53.0).Should().BeFalse();
    }

    [Fact]
    public void RecipeManager_ApplyRecipe_OverridesPipelineParameters()
    {
        var graph = new PipelineGraph();
        var tool = new DummyInspectionTool();
        var node = new PipelineNode("NodeA", tool);
        graph.AddNode(node);

        // Default value is 128.0
        tool.ParamThreshold.GetValue<double>().Should().Be(128.0);

        var manager = new RecipeManager();
        var recipe = new Recipe { Name = "SpecialBatch", ProductCode = "SB-01" };
        recipe.SetParameter("NodeA", "Threshold", 200.0);
        recipe.SetParameter("NodeA", "Gain", 1.8);
        manager.AddRecipe(recipe);

        bool switched = manager.SwitchRecipe("SB-01", graph);
        switched.Should().BeTrue();
        manager.ActiveRecipe.Should().NotBeNull();
        manager.ActiveRecipe!.Name.Should().Be("SpecialBatch");

        // Tool parameters should have updated
        tool.ParamThreshold.GetValue<double>().Should().Be(200.0);
        tool.ParamGain.GetValue<double>().Should().Be(1.8);
    }

    [Fact]
    public void RecipeManager_DiffRecipes_IdentifiesDifferences()
    {
        var r1 = new Recipe { Name = "R1" };
        r1.SetParameter("N1", "Threshold", 100);
        r1.SetParameter("N1", "Gain", 1.0);

        var r2 = new Recipe { Name = "R2" };
        r2.SetParameter("N1", "Threshold", 150); // different
        r2.SetParameter("N1", "Gain", 1.0);     // same
        r2.SetParameter("N2", "Filter", "Gaussian"); // extra

        var manager = new RecipeManager();
        var diffs = manager.DiffRecipes(r1, r2);

        diffs.Should().HaveCount(2);
        diffs.Should().Contain(d => d.NodeId == "N1" && d.ParameterName == "Threshold");
        diffs.Should().Contain(d => d.NodeId == "N2" && d.ParameterName == "Filter");
    }

    [Fact]
    public void RecipeManager_ValidateRecipe_CatchesMissingNodes()
    {
        var graph = new PipelineGraph();
        var node = new PipelineNode("ValidNode", new DummyInspectionTool());
        graph.AddNode(node);

        var recipe = new Recipe { Name = "BadRecipe" };
        recipe.SetParameter("GhostNode", "Threshold", 100);

        var manager = new RecipeManager();
        var result = manager.ValidateRecipe(recipe, graph);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("GhostNode"));
    }
}
