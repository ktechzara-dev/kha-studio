using VisionStudio.Core.Models;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;
using VisionStudio.Core.Types;

namespace VisionStudio.Imaging.Geometry;

[VisionTool("Geometry.RegionBoolean", "Region Boolean", "Geometry / ROI", Description = "Performs boolean set operations (Union, Intersection, Difference, Xor) between two Regions")]
public sealed class RegionBooleanTool : VisionToolBase
{
    public const string PortInRegionA = "RegionA";
    public const string PortInRegionB = "RegionB";
    public const string PortOutRegion = "RegionOut";
    public const string PortOutArea = "Area";
    public const string PortOutMask = "MaskOut";

    public const string ParamOperation = "Operation";
    public const string ParamRenderMask = "RenderMask";
    public const string ParamMaskWidth = "MaskWidth";
    public const string ParamMaskHeight = "MaskHeight";

    private readonly ToolParameter _pOperation;
    private readonly ToolParameter _pRenderMask;
    private readonly ToolParameter _pMaskWidth;
    private readonly ToolParameter _pMaskHeight;

    public RegionBooleanTool() : base("Geometry.RegionBoolean", "Region Boolean", "Geometry / ROI", "Boolean set operations on Regions")
    {
        _pOperation = AddParameter(new ToolParameter(ParamOperation, "Operation", ParameterType.Enum, "Intersection", enumOptions: new[] { "Intersection", "Union", "Difference", "Xor" }));
        _pRenderMask = AddParameter(new ToolParameter(ParamRenderMask, "Render Mask Output", ParameterType.Boolean, true));
        _pMaskWidth = AddParameter(new ToolParameter(ParamMaskWidth, "Mask Width", ParameterType.Integer, 640, min: 1, max: 10000));
        _pMaskHeight = AddParameter(new ToolParameter(ParamMaskHeight, "Mask Height", ParameterType.Integer, 480, min: 1, max: 10000));

        AddInput(new ToolPort(PortInRegionA, "Region A", PortType.Region, PortDirection.Input));
        AddInput(new ToolPort(PortInRegionB, "Region B", PortType.Region, PortDirection.Input));

        AddOutput(new ToolPort(PortOutRegion, "Region Out", PortType.Region, PortDirection.Output));
        AddOutput(new ToolPort(PortOutArea, "Area (px)", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortOutMask, "Mask Image", PortType.Image, PortDirection.Output, IsRequired: false));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        if (!ctx.Inputs.TryGetValue(PortInRegionA, out var objA) || objA is not Region regA)
        {
            return ToolResult.Failed("Missing Region A input.");
        }
        if (!ctx.Inputs.TryGetValue(PortInRegionB, out var objB) || objB is not Region regB)
        {
            return ToolResult.Failed("Missing Region B input.");
        }

        string op = _pOperation.GetValue<string>() ?? "Intersection";
        Region result = op switch
        {
            "Union" => regA.Union(regB),
            "Difference" => regA.Difference(regB),
            "Xor" => regA.Xor(regB),
            _ => regA.Intersect(regB)
        };

        var outputs = new Dictionary<string, object?>
        {
            [PortOutRegion] = result,
            [PortOutArea] = result.Area
        };

        if (_pRenderMask.GetValue<bool>())
        {
            int w = _pMaskWidth.GetValue<int>();
            int h = _pMaskHeight.GetValue<int>();
            var maskMat = result.ToMask(w, h);
            outputs[PortOutMask] = new ImageFrame(maskMat, format: PixelFormat.Mono8);
        }

        return ToolResult.Ok(outputs, message: $"{op} completed (Area={result.Area:F0} px).");
    }
}
