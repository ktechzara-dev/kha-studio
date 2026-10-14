using VisionStudio.Core.Flow;
using VisionStudio.Core.Tools;

namespace VisionStudio.Core.Tests.Tools;

public class ToolRegistryTests
{
    [Fact]
    public void ToolRegistry_RegistersToolsAndCreatesFreshInstances()
    {
        var registry = new ToolRegistry();
        var count = registry.RegisterAssembly(typeof(BranchTool).Assembly);

        count.Should().BeGreaterThan(0);
        registry.HasTool("Flow.Branch").Should().BeTrue();
        registry.HasTool("Flow.Counter").Should().BeTrue();
        registry.HasTool("Flow.Expression").Should().BeTrue();
        registry.HasTool("Output.Judge").Should().BeTrue();

        var instance1 = registry.CreateInstance("Flow.Branch");
        var instance2 = registry.CreateInstance("Flow.Branch");

        instance1.Should().NotBeNull();
        instance2.Should().NotBeNull();
        instance1.Should().NotBeSameAs(instance2);
        instance1.Id.Should().Be("Flow.Branch");
    }

    [Fact]
    public void GetAvailableTools_ReturnsOrderedCategories()
    {
        var registry = new ToolRegistry();
        registry.RegisterAssembly(typeof(BranchTool).Assembly);

        var list = registry.GetAvailableTools();
        list.Should().NotBeEmpty();
        list.Select(t => t.Category).Should().Contain("Flow Control");
    }
}
