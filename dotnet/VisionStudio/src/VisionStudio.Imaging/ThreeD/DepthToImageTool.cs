using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Overlays;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.ThreeD;

public enum DepthColorMap
{
    Grayscale = 0,
    Jet = 1,
    Turbo = 2,
    Viridis = 3
}

/// <summary>
/// Converts 16-bit or 32-bit floating-point depth maps into 8-bit 2D normalized images for downstream 2D inspection tools.
/// </summary>
[VisionTool("ThreeD.DepthToImage", "Depth Map to 2D Image", "3D Vision", Description = "Normalizes 16U/32F depth data into 8-bit 2D grayscale or pseudocolor image")]
public sealed class DepthToImageTool : VisionToolBase
{
    public const string PortInDepth = "DepthIn";
    public const string PortOutImage = "ImageOut";

    public const string ParamColorMap = "ColorMap";
    public const string ParamMinDepth = "MinDepth";
    public const string ParamMaxDepth = "MaxDepth";
    public const string ParamAutoRange = "AutoRange";

    private readonly ToolParameter _pColorMap;
    private readonly ToolParameter _pAutoRange;
    private readonly ToolParameter _pMinDepth;
    private readonly ToolParameter _pMaxDepth;

    public DepthToImageTool() : base("ThreeD.DepthToImage", "Depth Map to 2D Image", "3D Vision", "Normalizes 16U/32F depth data into 8-bit 2D grayscale or pseudocolor image")
    {
        _pColorMap = AddParameter(new ToolParameter(ParamColorMap, "Color Map", ParameterType.Enum, "Grayscale", enumOptions: new[] { "Grayscale", "Jet", "Turbo", "Viridis" }));
        _pAutoRange = AddParameter(new ToolParameter(ParamAutoRange, "Auto Range", ParameterType.Boolean, true));
        _pMinDepth = AddParameter(new ToolParameter(ParamMinDepth, "Min Depth (mm)", ParameterType.Double, 0.0, min: 0.0, max: 10000.0));
        _pMaxDepth = AddParameter(new ToolParameter(ParamMaxDepth, "Max Depth (mm)", ParameterType.Double, 1000.0, min: 0.0, max: 10000.0));

        AddInput(new ToolPort(PortInDepth, "Raw Depth Map", PortType.Image, PortDirection.Input));
        AddOutput(new ToolPort(PortOutImage, "Normalized 8-bit Image", PortType.Image, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext context, CancellationToken ct)
    {
        var depthFrame = context.GetImage(PortInDepth);
        if (depthFrame == null || depthFrame.Mat.Empty())
            return ToolResult.Failed("Depth map input is empty.");

        var src = depthFrame.Mat;
        var normalized8U = new Mat();

        bool auto = _pAutoRange.GetValue<bool>();
        if (auto)
        {
            Cv2.Normalize(src, normalized8U, 0, 255, NormTypes.MinMax, dtype: (int)MatType.CV_8U);
        }
        else
        {
            double minD = _pMinDepth.GetValue<double>();
            double maxD = _pMaxDepth.GetValue<double>();
            double range = Math.Max(0.001, maxD - minD);

            src.ConvertTo(normalized8U, MatType.CV_8UC1, 255.0 / range, -minD * 255.0 / range);
        }

        var colorMapStr = _pColorMap.GetValue<string>() ?? "Grayscale";
        Mat resultMat;

        if (colorMapStr.Equals("Grayscale", StringComparison.OrdinalIgnoreCase))
        {
            resultMat = normalized8U;
        }
        else
        {
            resultMat = new Mat();
            var cvColorMap = colorMapStr.ToLowerInvariant() switch
            {
                "jet" => ColormapTypes.Jet,
                "turbo" => ColormapTypes.Turbo,
                "viridis" => ColormapTypes.Viridis,
                _ => ColormapTypes.Jet
            };
            Cv2.ApplyColorMap(normalized8U, resultMat, cvColorMap);
            normalized8U.Dispose();
        }

        var overlays = new List<IOverlayPrimitive>
        {
            new TextOverlay($"Depth Rendered ({colorMapStr})", new Point2f(10, 25), new Scalar(0, 229, 255), FontScale: 0.6)
        };

        var outputs = new Dictionary<string, object?>
        {
            [PortOutImage] = new ImageFrame(resultMat, depthFrame.CameraId, depthFrame.FrameId)
        };

        return ToolResult.Ok(outputs, overlays, message: $"Depth rendered ({colorMapStr})");
    }
}
