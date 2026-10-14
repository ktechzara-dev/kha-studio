using System.ComponentModel;
using FluentAssertions;
using VisionStudio.App.Services;
using Xunit;

namespace VisionStudio.App.Tests;

public class LocalizationAndGuideTests
{
    [Fact]
    public void LocalizationService_CanToggleLanguage_AndNotifiesSubscribers()
    {
        var loc = LocalizationService.Instance;
        loc.CurrentLanguage = AppLanguage.Vietnamese;
        loc.CurrentLanguage.Should().Be(AppLanguage.Vietnamese);
        loc["App_Title"].Should().Contain("VisionStudio");
        loc.SwitchLanguageButtonText.Should().Contain("Tiếng Việt");

        bool propertyChangedFired = false;
        PropertyChangedEventHandler handler = (s, e) =>
        {
            if (e.PropertyName == "Item[]")
                propertyChangedFired = true;
        };

        loc.PropertyChanged += handler;
        try
        {
            loc.ToggleLanguage();
            loc.CurrentLanguage.Should().Be(AppLanguage.English);
            loc["App_Title"].Should().Contain("Industrial Machine Vision");
            loc.SwitchLanguageButtonText.Should().Contain("English");
            propertyChangedFired.Should().BeTrue();
        }
        finally
        {
            loc.PropertyChanged -= handler;
            // Restore back to Vietnamese
            loc.CurrentLanguage = AppLanguage.Vietnamese;
        }
    }

    [Fact]
    public void ToolGuideService_ProvidesComprehensiveGuides_ForAllStandardTools()
    {
        var standardTools = new[]
        {
            "Acquisition.CameraGrab",
            "Measurement.Caliper",
            "Color.Grayscale",
            "Filter.Gaussian",
            "Edges.Canny",
            "Blob.Analysis",
            "Identification.BarcodeReader",
            "Matching.TemplateMatch",
            "Defect.GoldenDiff"
        };

        foreach (var toolId in standardTools)
        {
            var guide = ToolGuideService.GetGuide(toolId, toolId, "TestCategory");
            guide.Should().NotBeNull();
            guide.ToolId.Should().Be(toolId);
            guide.DisplayNameVi.Should().NotBeNullOrWhiteSpace();
            guide.DisplayNameEn.Should().NotBeNullOrWhiteSpace();
            guide.PrincipleVi.Should().NotBeNullOrWhiteSpace();
            guide.PrincipleEn.Should().NotBeNullOrWhiteSpace();
            guide.PortsGuideVi.Should().NotBeNullOrWhiteSpace();
            guide.PortsGuideEn.Should().NotBeNullOrWhiteSpace();
            guide.ParametersGuideVi.Should().NotBeNullOrWhiteSpace();
            guide.ParametersGuideEn.Should().NotBeNullOrWhiteSpace();
            guide.IndustrialUseCasesVi.Should().NotBeNullOrWhiteSpace();
            guide.IndustrialUseCasesEn.Should().NotBeNullOrWhiteSpace();
            guide.QuickTipsVi.Should().NotBeNullOrWhiteSpace();
            guide.QuickTipsEn.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void ToolGuideService_GeneratesDynamicFallback_ForUnknownTool()
    {
        var guide = ToolGuideService.GetGuide("custom_neural_defect", "Phát Hiện Lỗi AI", "AI Deep Learning");
        guide.Should().NotBeNull();
        guide.ToolId.Should().Be("custom_neural_defect");
        guide.DisplayNameVi.Should().Be("Phát Hiện Lỗi AI");
        guide.CategoryVi.Should().Be("AI Deep Learning");
        guide.PrincipleVi.Should().NotBeNullOrWhiteSpace();
        guide.PortsGuideVi.Should().Contain("Input");
    }

    [Fact]
    public void ToolGuideService_GetAllCatalogGuides_ReturnsPopulatedList()
    {
        var catalog = ToolGuideService.GetAllCatalogGuides();
        catalog.Should().NotBeEmpty();
        catalog.Count.Should().BeGreaterThanOrEqualTo(9);
    }
}
