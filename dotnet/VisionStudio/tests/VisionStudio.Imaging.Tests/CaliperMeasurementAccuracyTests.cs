using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Tools;
using VisionStudio.Imaging.Measurement;

namespace VisionStudio.Imaging.Tests;

public class CaliperMeasurementAccuracyTests
{
    [Fact]
    public void CaliperTool_MeasuresKnownEdgePairWidth_Within_0_1_Pixel_Accuracy()
    {
        // ARRANGE: Synthesize calibrated optical target with exact ground truth width
        // Image size: 300x120. Background = 30.
        // Machined Part rectangle: X from 70.0 to 190.0 (Ground Truth Width = 120.000 px), Intensity = 220.
        const int width = 300;
        const int height = 120;
        const double groundTruthLeftX = 70.0;
        const double groundTruthRightX = 190.0;
        const double expectedWidthPx = groundTruthRightX - groundTruthLeftX; // 120.000 px

        using var mat = new Mat(height, width, MatType.CV_8UC1, new Scalar(30));
        var partRect = new Rect((int)groundTruthLeftX, 20, (int)expectedWidthPx, 80);
        Cv2.Rectangle(mat, partRect, new Scalar(220), -1);

        // Apply slight Gaussian optical PSF (Point Spread Function) mimicking real industrial lens diffraction
        using var blurred = new Mat();
        Cv2.GaussianBlur(mat, blurred, new Size(3, 3), 0.8);

        using var frame = new ImageFrame(blurred, "caliper-target", 1);

        var tool = new CaliperTool();
        tool.Parameters.First(p => p.Name == CaliperTool.ParamX1).Value = 20.0;
        tool.Parameters.First(p => p.Name == CaliperTool.ParamY1).Value = 60.0;
        tool.Parameters.First(p => p.Name == CaliperTool.ParamX2).Value = 260.0;
        tool.Parameters.First(p => p.Name == CaliperTool.ParamY2).Value = 60.0;
        tool.Parameters.First(p => p.Name == CaliperTool.ParamThreshold).Value = 15.0;

        var ctx = new ToolContext(new Dictionary<string, object?> { [CaliperTool.PortInImage] = frame });

        // ACT
        using var result = tool.Execute(ctx, CancellationToken.None);

        // ASSERT
        result.Status.Should().Be(ToolStatus.Ok);
        var measuredWidth = result.GetOutput<double>(CaliperTool.PortWidthPx);
        var e1x = result.GetOutput<double>(CaliperTool.PortEdge1X);
        var e2x = result.GetOutput<double>(CaliperTool.PortEdge2X);

        // Strict acceptance: within 0.1 pixel of ground truth
        var error = Math.Abs(measuredWidth - expectedWidthPx);
        error.Should().BeLessThan(0.1, because: $"Sub-pixel caliper error must be strictly under 0.1 px (actual error was {error:F4} px)");

        // Ground truth edge boundaries in discrete image coordinates lie on pixel interface:
        // Left transition between px 69 and 70 is at 69.5; right transition between px 189 and 190 is at 189.5
        const double trueLeftBoundary = groundTruthLeftX - 0.5; // 69.5
        const double trueRightBoundary = groundTruthRightX - 0.5; // 189.5

        Math.Abs(e1x - trueLeftBoundary).Should().BeLessThan(0.1);
        Math.Abs(e2x - trueRightBoundary).Should().BeLessThan(0.1);
        result.Overlays.Should().NotBeEmpty();
    }
}
