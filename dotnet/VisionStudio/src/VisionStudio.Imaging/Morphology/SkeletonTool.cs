using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Morphology;

/// <summary>
/// Thins binary shapes to single-pixel topological skeletons via morphological erosion-opening decomposition.
/// </summary>
[VisionTool("Morphology.Skeleton", "Skeleton", "Morphology", Description = "Reduces binary shapes to 1-pixel centerline skeletons")]
public sealed class SkeletonTool : VisionToolBase
{
    public const string PortInImage = "ImageIn";
    public const string PortOutImage = "ImageOut";

    public SkeletonTool() : base("Morphology.Skeleton", "Skeleton", "Morphology", "Centerline skeletonization")
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

        using var binary = new Mat();
        Cv2.Threshold(gray, binary, 127, 255, ThresholdTypes.Binary);

        var skeleton = new Mat(binary.Size(), MatType.CV_8U, Scalar.All(0));
        using var element = Cv2.GetStructuringElement(MorphShapes.Cross, new Size(3, 3));

        using var temp = new Mat();
        using var eroded = new Mat();
        var done = false;

        while (!done && !ct.IsCancellationRequested)
        {
            Cv2.MorphologyEx(binary, temp, MorphTypes.Open, element);
            Cv2.Subtract(binary, temp, temp);
            Cv2.BitwiseOr(skeleton, temp, skeleton);
            Cv2.Erode(binary, binary, element);

            if (Cv2.CountNonZero(binary) == 0)
            {
                done = true;
            }
        }

        var outFrame = new ImageFrame(skeleton, inputFrame.CameraId, inputFrame.FrameId, PixelFormat.Mono8);
        return ToolResult.Ok(new Dictionary<string, object?> { [PortOutImage] = outFrame }, message: "Extracted topological skeleton");
    }
}
