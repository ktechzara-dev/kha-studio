using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Overlays;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Edges;

/// <summary>
/// Extracts edge locations with sub-pixel precision (under 0.1 pixel resolution) using parabolic directional gradient fitting.
/// </summary>
[VisionTool("Edges.SubPixel", "Sub-Pixel Edge Detector", "Edges and Contours", Description = "Extracts edge points with sub-pixel parabolic interpolation")]
public sealed class SubPixelEdgeTool : VisionToolBase
{
    public const string ParamThreshold = "GradientThreshold";
    public const string ParamMaxPoints = "MaxEdgePoints";

    public const string PortInImage = "ImageIn";
    public const string PortPointCount = "PointCount";
    public const string PortAnnotatedImage = "AnnotatedImage";

    private readonly ToolParameter _pThresh;
    private readonly ToolParameter _pMaxPts;

    public SubPixelEdgeTool() : base("Edges.SubPixel", "Sub-Pixel Edge Detector", "Edges and Contours", "Sub-pixel edge extractor")
    {
        _pThresh = AddParameter(new ToolParameter(ParamThreshold, "Min Gradient Threshold", ParameterType.Double, 30.0, min: 5.0, max: 255.0));
        _pMaxPts = AddParameter(new ToolParameter(ParamMaxPoints, "Max Points to Sample", ParameterType.Integer, 200, min: 10, max: 5000));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));
        AddOutput(new ToolPort(PortPointCount, "Point Count", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortAnnotatedImage, "Annotated Image", PortType.Image, PortDirection.Output));
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

        // Compute horizontal and vertical Sobel gradients
        using var gx = new Mat();
        using var gy = new Mat();
        Cv2.Sobel(gray, gx, MatType.CV_32F, 1, 0, 3);
        Cv2.Sobel(gray, gy, MatType.CV_32F, 0, 1, 3);

        using var mag = new Mat();
        Cv2.Magnitude(gx, gy, mag);

        var thresh = (float)_pThresh.GetValue<double>();
        var maxPts = _pMaxPts.GetValue<int>();

        var subPixelPoints = new List<Point2f>();
        var step = Math.Max(1, (gray.Rows * gray.Cols) / (maxPts * 50));

        for (int r = 1; r < gray.Rows - 1; r += 2)
        {
            for (int c = 1; c < gray.Cols - 1; c += 2)
            {
                var g = mag.Get<float>(r, c);
                if (g < thresh) continue;

                var dx = gx.Get<float>(r, c);
                var dy = gy.Get<float>(r, c);

                // Check local 1D peak along dominant gradient direction
                if (Math.Abs(dx) >= Math.Abs(dy))
                {
                    var gLeft = mag.Get<float>(r, c - 1);
                    var gRight = mag.Get<float>(r, c + 1);

                    if (g >= gLeft && g >= gRight && (gLeft != gRight || g > gLeft))
                    {
                        // Parabolic peak delta: offset = (left - right) / (2 * (left - 2*center + right))
                        var denom = 2.0f * (gLeft - 2f * g + gRight);
                        var offset = Math.Abs(denom) > 1e-4 ? (gLeft - gRight) / denom : 0f;
                        offset = Math.Clamp(offset, -0.5f, 0.5f);

                        subPixelPoints.Add(new Point2f(c + offset, r));
                        if (subPixelPoints.Count >= maxPts) break;
                    }
                }
                else
                {
                    var gUp = mag.Get<float>(r - 1, c);
                    var gDown = mag.Get<float>(r + 1, c);

                    if (g >= gUp && g >= gDown && (gUp != gDown || g > gUp))
                    {
                        var denom = 2.0f * (gUp - 2f * g + gDown);
                        var offset = Math.Abs(denom) > 1e-4 ? (gUp - gDown) / denom : 0f;
                        offset = Math.Clamp(offset, -0.5f, 0.5f);

                        subPixelPoints.Add(new Point2f(c, r + offset));
                        if (subPixelPoints.Count >= maxPts) break;
                    }
                }
            }
            if (subPixelPoints.Count >= maxPts) break;
        }

        var annotated = new Mat();
        if (src.Channels() == 1)
        {
            Cv2.CvtColor(src, annotated, ColorConversionCodes.GRAY2BGR);
        }
        else
        {
            src.CopyTo(annotated);
        }

        var overlays = new List<IOverlayPrimitive>();
        foreach (var pt in subPixelPoints)
        {
            var ov = new CrossOverlay(pt, Size: 4f, Color: new Scalar(0, 0, 255), Thickness: 1);
            overlays.Add(ov);
            ov.Render(annotated);
        }

        var outFrame = new ImageFrame(annotated, inputFrame.CameraId, inputFrame.FrameId, PixelFormat.Bgr24);
        var outputs = new Dictionary<string, object?>
        {
            [PortPointCount] = subPixelPoints.Count,
            [PortAnnotatedImage] = outFrame
        };

        return ToolResult.Ok(outputs, overlays, message: $"Detected {subPixelPoints.Count} sub-pixel edge points");
    }
}
