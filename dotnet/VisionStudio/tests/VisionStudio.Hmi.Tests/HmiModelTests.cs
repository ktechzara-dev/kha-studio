using VisionStudio.Hmi.Model;
using Xunit;

namespace VisionStudio.Hmi.Tests;

public class HmiModelTests
{
    [Fact]
    public void CanCreateDefaultProjectAndSerializeToJson()
    {
        // Arrange
        var project = HmiSerializer.CreateDefaultProject();
        Assert.NotNull(project);
        Assert.Equal(3, project.Pages.Count);

        // Act
        var json = HmiSerializer.Serialize(project);
        Assert.False(string.IsNullOrWhiteSpace(json));

        var restored = HmiSerializer.Deserialize(json);

        // Assert
        Assert.NotNull(restored);
        Assert.Equal(project.Title, restored.Title);
        Assert.Equal(project.MachineId, restored.MachineId);
        Assert.Equal(project.Pages.Count, restored.Pages.Count);

        var overview = restored.Pages[0];
        Assert.Equal("page-overview", overview.Id);
        Assert.True(overview.Widgets.Count >= 7);

        var liveWidget = overview.Widgets.First(w => w.Type == HmiWidgetType.LiveImage);
        Assert.NotNull(liveWidget.Binding);
        Assert.Equal("Station1.Image", liveWidget.Binding.Path);
    }

    [Fact]
    public void DefaultPageSelectionResolvesCorrectly()
    {
        var project = HmiSerializer.CreateDefaultProject();
        var defaultPage = project.GetDefaultPage();

        Assert.NotNull(defaultPage);
        Assert.Equal("page-overview", defaultPage.Id);

        project.DefaultPageId = "page-spc";
        Assert.Equal("page-spc", project.GetDefaultPage()?.Id);
    }
}
