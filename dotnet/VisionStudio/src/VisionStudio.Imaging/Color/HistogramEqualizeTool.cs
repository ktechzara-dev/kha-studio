using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Color;

/// <summary>
/// Enhances image contrast using standard global histogram equalization or CLAHE.
/// </summary>
[VisionTool("Color.HistogramEqualize", "Histogram Equalize / CLAHE", "Color / Intensity", Description = "Balances dynamic range via global equalization or CLAHE")]
public sealed class HistogramEqualizeTool : VisionToolBase
{
    public const string ParamMethod = "Method";
    public const string ParamClipLimit = "ClaheClipLimit";
    public const string ParamGridSize = "ClaheTileSize";

    public const string PortInImage = "ImageIn";
    public const string PortOutImage = "ImageOut";

    private readonly ToolParameter _pMethod;
    private readonly ToolParameter _pClip;
    private readonly ToolParameter _pTile;

    public HistogramEqualizeTool() : base("Color.HistogramEqualize", "Histogram Equalize / CLAHE", "Color / Intensity", "Enhances image contrast")
    {
        _pMethod = AddParameter(new ToolParameter(ParamMethod, "Method", ParameterType.Enum, "CLAHE", enumOptions: new[] { "CLAHE", "Standard" }));
        _pClip = AddParameter(new ToolParameter(ParamClipLimit, "CLAHE Clip Limit", ParameterType.Double, 2.0, min: 0.5, max: 40.0));
        _pTile = AddParameter(new ToolParameter(ParamGridSize, "CLAHE Tile Grid Size", ParameterType.Integer, 8, min: 2, max: 64));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));
        AddOutput(new ToolPort(PortOutImage, "Image Out", PortType.Image, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var inputFrame = ctx.GetImage(PortInImage);
        if (inputFrame == null) return ToolResult.Failed("Missing input image.");

        var src = inputFrame.Mat;
        var method = _pMethod.GetValue<string>() ?? "CLAHE";
        var isClahe = method.Equals("CLAHE", StringComparison.OrdinalIgnoreCase);

        var dst = new Mat();

        if (src.Channels() == 1)
        {
            if (isClahe)
            {
                var clip = _pClip.GetValue<double>();
                var tile = _pTile.GetValue<int>();
                using var clahe = Cv2.CreateCLAHE(clip, new Size(tile, tile));
                clahe.Apply(src, dst);
            }
            else
            {
                Cv2.EqualizeHist(src, dst);
            }
        }
        else
        {
            // For color images, convert to YCrCb, equalize Y channel, and convert back
            using var ycrcb = new Mat();
            Cv2.CvtColor(src, ycrcb, ColorConversionCodes.BGR2YCrCb);
            var channels = Cv2.Split(ycrcb);

            if (isClahe)
            {
                var clip = _pClip.GetValue<double>();
                var tile = _pTile.GetValue<int>();
                using var clahe = Cv2.CreateCLAHE(clip, new Size(tile, tile));
                using var eqY = new Mat();
                clahe.Apply(channels[0], eqY);
                channels[0].Dispose();
                channels[0] = eqY.Clone();
            }
            else
            {
                using var eqY = new Mat();
                Cv2.EqualizeHist(channels[0], eqY);
                channels[0].Dispose();
                channels[0] = eqY.Clone();
            }

            using var merged = new Mat();
            Cv2.Merge(channels, merged);
            Cv2.CvtColor(merged, dst, ColorConversionCodes.YCrCb2BGR);

            foreach (var ch in channels) ch.Dispose();
        }

        var outFrame = new ImageFrame(dst, inputFrame.CameraId, inputFrame.FrameId, inputFrame.Format);
        return ToolResult.Ok(new Dictionary<string, object?> { [PortOutImage] = outFrame }, message: $"Equalized using {method}");
    }
}
