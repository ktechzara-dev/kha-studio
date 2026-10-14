using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Overlays;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Geometry;

/// <summary>
/// Defines interactive inspection Region of Interest (Rectangle, Circle, Ring, Polygon) and outputs binary mask.
/// </summary>
[VisionTool("Geometry.Roi", "ROI Mask", "Geometry / ROI", Description = "Creates a masked region of interest (Rectangle, Circle, Ring, Polygon)")]
public sealed class RoiTool : VisionToolBase
{
    public const string ParamShape = "Shape";
    public const string ParamCenterX = "CenterX";
    public const string ParamCenterY = "CenterY";
    public const string ParamRadius = "Radius";
    public const string ParamInnerRadius = "InnerRadius";
    public const string ParamWidth = "Width";
    public const string ParamHeight = "Height";

    public const string PortInImage = "ImageIn";
    public const string PortMask = "Mask";
    public const string PortMaskedImage = "MaskedImage";

    private readonly ToolParameter _pShape;
    private readonly ToolParameter _pCx;
    private readonly ToolParameter _pCy;
    private readonly ToolParameter _pRadius;
    private readonly ToolParameter _pInnerRadius;
    private readonly ToolParameter _pW;
    private readonly ToolParameter _pH;

    public RoiTool() : base("Geometry.Roi", "ROI Mask", "Geometry / ROI", "Generates mask for ROI")
    {
        _pShape = AddParameter(new ToolParameter(
            ParamShape, "Shape", ParameterType.Enum, "Rectangle",
            enumOptions: new[] { "Rectangle", "Circle", "Ring" }));

        _pCx = AddParameter(new ToolParameter(ParamCenterX, "Center X", ParameterType.Integer, 160));
        _pCy = AddParameter(new ToolParameter(ParamCenterY, "Center Y", ParameterType.Integer, 120));
        _pRadius = AddParameter(new ToolParameter(ParamRadius, "Outer Radius", ParameterType.Integer, 80, min: 1));
        _pInnerRadius = AddParameter(new ToolParameter(ParamInnerRadius, "Inner Radius (Ring)", ParameterType.Integer, 30, min: 0));
        _pW = AddParameter(new ToolParameter(ParamWidth, "Width", ParameterType.Integer, 160, min: 1));
        _pH = AddParameter(new ToolParameter(ParamHeight, "Height", ParameterType.Integer, 120, min: 1));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));
        AddOutput(new ToolPort(PortMask, "Mask", PortType.Image, PortDirection.Output));
        AddOutput(new ToolPort(PortMaskedImage, "Masked Image", PortType.Image, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var inputFrame = ctx.GetImage(PortInImage);
        if (inputFrame == null) return ToolResult.Failed("Missing input image.");

        var src = inputFrame.Mat;
        var mask = new Mat(src.Rows, src.Cols, MatType.CV_8UC1, new Scalar(0));
        var shape = _pShape.GetValue<string>() ?? "Rectangle";
        var cx = _pCx.GetValue<int>();
        var cy = _pCy.GetValue<int>();

        var overlays = new List<IOverlayPrimitive>();

        switch (shape.ToLowerInvariant())
        {
            case "circle":
                var r = _pRadius.GetValue<int>();
                Cv2.Circle(mask, new Point(cx, cy), r, new Scalar(255), -1);
                overlays.Add(new CircleOverlay(new Point2f(cx, cy), r, new Scalar(0, 255, 0), Thickness: 2));
                break;

            case "ring":
                var rOut = _pRadius.GetValue<int>();
                var rIn = _pInnerRadius.GetValue<int>();
                Cv2.Circle(mask, new Point(cx, cy), rOut, new Scalar(255), -1);
                Cv2.Circle(mask, new Point(cx, cy), rIn, new Scalar(0), -1);
                overlays.Add(new CircleOverlay(new Point2f(cx, cy), rOut, new Scalar(0, 255, 0), Thickness: 2));
                overlays.Add(new CircleOverlay(new Point2f(cx, cy), rIn, new Scalar(0, 165, 255), Thickness: 2));
                break;

            default: // Rectangle
                var w = _pW.GetValue<int>();
                var h = _pH.GetValue<int>();
                var rect = new Rect(cx - w / 2, cy - h / 2, w, h);
                rect = rect.Intersect(new Rect(0, 0, src.Cols, src.Rows));
                if (rect.Width > 0 && rect.Height > 0)
                {
                    Cv2.Rectangle(mask, rect, new Scalar(255), -1);
                    overlays.Add(new RectOverlay(new Rect2f(rect.X, rect.Y, rect.Width, rect.Height), new Scalar(0, 255, 0), Thickness: 2));
                }
                break;
        }

        var maskedMat = new Mat();
        src.CopyTo(maskedMat, mask);

        var maskFrame = new ImageFrame(mask, inputFrame.CameraId, inputFrame.FrameId, PixelFormat.Mono8);
        var maskedFrame = new ImageFrame(maskedMat, inputFrame.CameraId, inputFrame.FrameId, inputFrame.Format);

        var outputs = new Dictionary<string, object?>
        {
            [PortMask] = maskFrame,
            [PortMaskedImage] = maskedFrame
        };

        return ToolResult.Ok(outputs, overlays, message: $"Generated ROI mask ({shape})");
    }
}
