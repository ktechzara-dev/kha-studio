using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Segmentation;

/// <summary>
/// Clusters color pixels into K dominant palette centroids via K-Means machine learning segmentation.
/// </summary>
[VisionTool("Segmentation.KMeans", "k-Means Color Segmentation", "Segmentation", Description = "Clusters image into K dominant color segments")]
public sealed class KMeansSegmentationTool : VisionToolBase
{
    public const string ParamK = "K";
    public const string ParamMaxIter = "MaxIterations";

    public const string PortInImage = "ImageIn";
    public const string PortOutImage = "ImageOut";

    private readonly ToolParameter _pK;
    private readonly ToolParameter _pMaxIter;

    public KMeansSegmentationTool() : base("Segmentation.KMeans", "k-Means Color Segmentation", "Segmentation", "K-Means color clustering")
    {
        _pK = AddParameter(new ToolParameter(ParamK, "Cluster Count (K)", ParameterType.Integer, 3, min: 2, max: 16));
        _pMaxIter = AddParameter(new ToolParameter(ParamMaxIter, "Max Iterations", ParameterType.Integer, 10, min: 1, max: 100));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));
        AddOutput(new ToolPort(PortOutImage, "Image Out", PortType.Image, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var inputFrame = ctx.GetImage(PortInImage);
        if (inputFrame == null) return ToolResult.Failed("Missing input image.");

        var src = inputFrame.Mat;
        using var colorSrc = new Mat();
        if (src.Channels() == 1)
        {
            Cv2.CvtColor(src, colorSrc, ColorConversionCodes.GRAY2BGR);
        }
        else
        {
            src.CopyTo(colorSrc);
        }

        var k = Math.Clamp(_pK.GetValue<int>(), 2, 16);
        var maxIter = Math.Max(1, _pMaxIter.GetValue<int>());

        // Reshape image to N x 3 float array
        using var samples = colorSrc.Reshape(3, colorSrc.Rows * colorSrc.Cols);
        using var samplesF = new Mat();
        samples.ConvertTo(samplesF, MatType.CV_32F);

        using var labels = new Mat();
        using var centers = new Mat();
        var criteria = new TermCriteria(CriteriaTypes.Eps | CriteriaTypes.MaxIter, maxIter, 1.0);

        Cv2.Kmeans(samplesF, k, labels, criteria, attempts: 2, flags: KMeansFlags.PpCenters, centers: centers);

        // Map cluster centers back to pixels
        using var segmentedF = new Mat(samplesF.Rows, 3, MatType.CV_32F);
        for (int i = 0; i < samplesF.Rows; i++)
        {
            var clusterIdx = labels.Get<int>(i, 0);
            segmentedF.Set(i, 0, centers.Get<float>(clusterIdx, 0));
            segmentedF.Set(i, 1, centers.Get<float>(clusterIdx, 1));
            segmentedF.Set(i, 2, centers.Get<float>(clusterIdx, 2));
        }

        using var segmented8U = new Mat();
        segmentedF.ConvertTo(segmented8U, MatType.CV_8U);
        var dst = segmented8U.Reshape(3, colorSrc.Rows);

        var outFrame = new ImageFrame(dst.Clone(), inputFrame.CameraId, inputFrame.FrameId, PixelFormat.Bgr24);
        return ToolResult.Ok(new Dictionary<string, object?> { [PortOutImage] = outFrame }, message: $"K-Means segmented into {k} color clusters");
    }
}
