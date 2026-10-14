using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Overlays;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.SamplePlugin;

/// <summary>
/// Sample custom third-party plugin tool demonstrating how developers extend VisionStudio via the Plugin SDK.
/// </summary>
[VisionTool("Plugin.CustomInvert", "Custom Invert Filter", "Custom Plugins", Description = "Inverts pixel intensities as a demonstration of the VisionStudio Plugin SDK")]
public sealed class CustomInvertTool : VisionToolBase
{
    public const string PortInImage = "ImageIn";
    public const string PortOutImage = "ImageOut";
    public const string ParamEnabled = "Enabled";

    private readonly ToolParameter _pEnabled;

    public CustomInvertTool() : base("Plugin.CustomInvert", "Custom Invert Filter", "Custom Plugins", "Inverts pixel intensities as a demonstration of the VisionStudio Plugin SDK")
    {
        _pEnabled = AddParameter(new ToolParameter(ParamEnabled, "Invert Enabled", ParameterType.Boolean, true));

        AddInput(new ToolPort(PortInImage, "Input Image", PortType.Image, PortDirection.Input));
        AddOutput(new ToolPort(PortOutImage, "Inverted Output Image", PortType.Image, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext context, CancellationToken ct)
    {
        var inputFrame = context.GetImage(PortInImage);
        if (inputFrame == null || inputFrame.Mat.Empty())
            return ToolResult.Failed("Input image is missing or empty.");

        bool enabled = _pEnabled.GetValue<bool>();
        Mat resultMat;

        if (enabled)
        {
            resultMat = new Mat();
            Cv2.BitwiseNot(inputFrame.Mat, resultMat);
        }
        else
        {
            resultMat = inputFrame.Mat.Clone();
        }

        var overlays = new List<IOverlayPrimitive>
        {
            new TextOverlay($"Plugin Invert: {(enabled ? "ACTIVE" : "BYPASS")}", new Point2f(10, 25), new Scalar(0, 210, 255), FontScale: 0.5)
        };

        var outputs = new Dictionary<string, object?>
        {
            [PortOutImage] = new ImageFrame(resultMat, inputFrame.CameraId, inputFrame.FrameId)
        };

        return ToolResult.Ok(outputs, overlays, message: $"Invert: {(enabled ? "Active" : "Bypassed")}");
    }
}
