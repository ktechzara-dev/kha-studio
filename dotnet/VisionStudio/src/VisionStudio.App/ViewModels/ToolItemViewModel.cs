using VisionStudio.App.Services;
using VisionStudio.Core.Tools;

namespace VisionStudio.App.ViewModels;

/// <summary>
/// ViewModel representing an available tool in the toolbox catalog with integrated documentation and visual diagrams.
/// </summary>
public sealed class ToolItemViewModel
{
    public ToolDescriptor Descriptor { get; }
    public string Id => Descriptor.Id;
    public string DisplayName => Descriptor.DisplayName;
    public string Category => Descriptor.Category;
    public string Description => Descriptor.Description;

    public ToolDocumentation Guide => ToolGuideService.GetGuide(Id, DisplayName, Category);
    public string LocalizedDisplayName => Guide.LocalizedDisplayName;
    public string LocalizedCategory => Guide.LocalizedCategory;
    public string LocalizedPrinciple => Guide.LocalizedPrinciple;
    public string LocalizedPorts => Guide.LocalizedPorts;
    public string LocalizedParameters => Guide.LocalizedParameters;
    public string LocalizedUseCases => Guide.LocalizedUseCases;
    public string LocalizedTips => Guide.LocalizedTips;
    public string IllustrationDiagram => Guide.IllustrationDiagram;

    public ToolItemViewModel(ToolDescriptor descriptor)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
    }
}
