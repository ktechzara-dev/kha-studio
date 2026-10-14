using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Overlays;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Geometry;

/// <summary>
/// Crops an axis-aligned rectangular sub-window from an input image.
/// </summary>
[VisionTool("Geometry.Crop", "Crop", "Geometry / ROI", Description = "Extracts an axis-aligned rectangular region")]
public sealed class CropTool : VisionToolBase
{
    public const string ParamX = "X";
    public const string ParamY = "Y";
    public const string ParamWidth = "Width";
    public const string ParamHeight = "Height";

    public const string PortInImage = "ImageIn";
    public const string PortOutImage = "ImageOut";

    private readonly ToolParameter _pX;
    private readonly ToolParameter _pY;
    private readonly ToolParameter _pW;
    private readonly ToolParameter _pH;

    public CropTool() : base("Geometry.Crop", "Crop", "Geometry / ROI", "Extracts rectangular sub-window")
    {
        _pX = AddParameter(new ToolParameter(ParamX, "Origin X", ParameterType.Integer, 0, min: 0));
        _pY = AddParameter(new ToolParameter(ParamY, "Origin Y", ParameterType.Integer, 0, min: 0));
        _pW = AddParameter(new ToolParameter(ParamWidth, "Width", ParameterType.Integer, 320, min: 1));
        _pH = AddParameter(new ToolParameter(ParamHeight, "Height", ParameterType.Integer, 240, min: 1));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));
        AddOutput(new ToolPort(PortOutImage, "Image Out", PortType.Image, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var inputFrame = ctx.GetImage(PortInImage);
        if (inputFrame == null) return ToolResult.Failed("Missing input image.");

        var src = inputFrame.Mat;
        var x = Math.Clamp(_pX.GetValue<int>(), 0, Math.Max(0, src.Cols - 1));
        var y = Math.Clamp(_pY.GetValue<int>(), 0, Math.Max(0, src.Rows - 1));
        var w = Math.Clamp(_pW.GetValue<int>(), 1, src.Cols - x);
        var h = Math.Clamp(_pH.GetValue<int>(), 1, src.Rows - y);

        var roiRect = new Rect(x, y, w, h);
        var croppedMat = new Mat(src, roiRect).Clone(); // Deep copy for isolation

        var outFrame = new ImageFrame(croppedMat, inputFrame.CameraId, inputFrame.FrameId, inputFrame.Format);
        var overlay = new RectOverlay(new Rect2f(x, y, w, h), new Scalar(0, 255, 255), Thickness: 2);

        var outputs = new Dictionary<string, object?> { [PortOutImage] = outFrame };
        return ToolResult.Ok(outputs, new[] { overlay }, message: $"Cropped [{x}, {y}, {w}, {h}]");
    }
}
