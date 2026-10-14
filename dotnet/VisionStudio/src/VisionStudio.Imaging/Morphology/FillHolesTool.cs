using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Morphology;

/// <summary>
/// Fills interior enclosed cavities/holes inside binary objects without distorting external perimeters.
/// </summary>
[VisionTool("Morphology.FillHoles", "Fill Holes", "Morphology", Description = "Fills enclosed internal cavities within binary blobs")]
public sealed class FillHolesTool : VisionToolBase
{
    public const string PortInImage = "ImageIn";
    public const string PortOutImage = "ImageOut";

    public FillHolesTool() : base("Morphology.FillHoles", "Fill Holes", "Morphology", "Fills interior holes")
    {
        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));
        AddOutput(new ToolPort(PortOutImage, "Image Out", PortType.Image, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var inputFrame = ctx.GetImage(PortInImage);
        if (inputFrame == null) return ToolResult.Failed("Missing input image.");

        var src = inputFrame.Mat;
        using var gray = new Mat();
        if (src.Channels() > 1)
        {
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        }
        else
        {
            src.CopyTo(gray);
        }

        // Flood fill from background corners (pad 2 pixels for flood mask)
        using var floodfilled = gray.Clone();
        using var mask = new Mat(gray.Rows + 2, gray.Cols + 2, MatType.CV_8U, Scalar.All(0));

        Cv2.FloodFill(floodfilled, mask, new Point(0, 0), new Scalar(255));

        using var floodfilledInv = new Mat();
        Cv2.BitwiseNot(floodfilled, floodfilledInv);

        var dst = new Mat();
        Cv2.BitwiseOr(gray, floodfilledInv, dst);

        var outFrame = new ImageFrame(dst, inputFrame.CameraId, inputFrame.FrameId, PixelFormat.Mono8);
        return ToolResult.Ok(new Dictionary<string, object?> { [PortOutImage] = outFrame }, message: "Filled interior holes");
    }
}
