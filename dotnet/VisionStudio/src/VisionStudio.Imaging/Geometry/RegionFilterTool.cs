using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;
using VisionStudio.Core.Types;

namespace VisionStudio.Imaging.Geometry;

[VisionTool("Geometry.RegionFilter", "Filter Region", "Geometry / ROI", Description = "Checks if a Region satisfies minimum and maximum area criteria")]
public sealed class RegionFilterTool : VisionToolBase
{
    public const string PortInRegion = "RegionIn";
    public const string PortOutRegion = "RegionOut";
    public const string PortOutPassed = "Passed";
    public const string PortOutArea = "Area";

    public const string ParamMinArea = "MinArea";
    public const string ParamMaxArea = "MaxArea";

    private readonly ToolParameter _pMinArea;
    private readonly ToolParameter _pMaxArea;

    public RegionFilterTool() : base("Geometry.RegionFilter", "Filter Region", "Geometry / ROI", "Filters region by geometric criteria")
    {
        _pMinArea = AddParameter(new ToolParameter(ParamMinArea, "Min Area (px)", ParameterType.Double, 100.0, min: 0.0, max: 10000000.0));
        _pMaxArea = AddParameter(new ToolParameter(ParamMaxArea, "Max Area (px)", ParameterType.Double, 500000.0, min: 1.0, max: 10000000.0));

        AddInput(new ToolPort(PortInRegion, "Region In", PortType.Region, PortDirection.Input));

        AddOutput(new ToolPort(PortOutRegion, "Region Out", PortType.Region, PortDirection.Output));
        AddOutput(new ToolPort(PortOutPassed, "Passed", PortType.Boolean, PortDirection.Output));
        AddOutput(new ToolPort(PortOutArea, "Area (px)", PortType.Number, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        if (!ctx.Inputs.TryGetValue(PortInRegion, out var obj) || obj is not Region reg)
        {
            return ToolResult.Failed("Missing Region input.");
        }

        double minArea = _pMinArea.GetValue<double>();
        double maxArea = _pMaxArea.GetValue<double>();

        double area = reg.Area;
        bool passed = area >= minArea && area <= maxArea;

        var outputs = new Dictionary<string, object?>
        {
            [PortOutRegion] = reg,
            [PortOutPassed] = passed,
            [PortOutArea] = area
        };

        return passed
            ? ToolResult.Ok(outputs, message: $"Area={area:F0} px (PASS).")
            : ToolResult.Warning($"Area={area:F0} px outside range [{minArea:F0}, {maxArea:F0}] (FAIL).", outputs);
    }
}
