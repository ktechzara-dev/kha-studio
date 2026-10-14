using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Tools;
using VisionStudio.Imaging.Matching;

namespace VisionStudio.Imaging.Tests;

public class MatchingToolsTests
{
    private static (ImageFrame Scene, ImageFrame Template) CreateSyntheticSceneAndTemplate()
    {
        var scene = new Mat(200, 200, MatType.CV_8UC1, new Scalar(30));
        // Draw recognizable template pattern at (80, 80)
        var tpl = new Mat(40, 40, MatType.CV_8UC1, new Scalar(200));
        Cv2.Circle(tpl, new Point(20, 20), 10, new Scalar(50), -1);

        // Stamp template into scene
        using var roi = new Mat(scene, new Rect(80, 80, 40, 40));
        tpl.CopyTo(roi);

        return (new ImageFrame(scene, "scene"), new ImageFrame(tpl, "template"));
    }

    [Fact]
    public void TemplateMatchTool_LocalizesTargetAccurately()
    {
        var (scene, tpl) = CreateSyntheticSceneAndTemplate();
        using (scene)
        using (tpl)
        {
            var tool = new TemplateMatchTool();
            var ctx = new ToolContext(new Dictionary<string, object?>
            {
                [TemplateMatchTool.PortInImage] = scene,
                [TemplateMatchTool.PortTemplate] = tpl
            });

            using var result = tool.Execute(ctx, CancellationToken.None);
            result.Status.Should().Be(ToolStatus.Ok);
            var score = result.GetOutput<double>(TemplateMatchTool.PortBestScore);
            score.Should().BeGreaterThan(0.95);

            var bx = result.GetOutput<double>(TemplateMatchTool.PortBestX);
            var by = result.GetOutput<double>(TemplateMatchTool.PortBestY);
            // Center of (80, 80) + 40/2 = (100, 100)
            bx.Should().Be(100.0);
            by.Should().Be(100.0);
        }
    }

    [Fact]
    public void RotationInvariantMatchTool_FindsRotatedInstance()
    {
        var (scene, tpl) = CreateSyntheticSceneAndTemplate();
        using (scene)
        using (tpl)
        {
            var tool = new RotationInvariantMatchTool();
            tool.Parameters.First(p => p.Name == RotationInvariantMatchTool.ParamMinAngle).Value = -20.0;
            tool.Parameters.First(p => p.Name == RotationInvariantMatchTool.ParamMaxAngle).Value = 20.0;
            tool.Parameters.First(p => p.Name == RotationInvariantMatchTool.ParamStepAngle).Value = 5.0;

            var ctx = new ToolContext(new Dictionary<string, object?>
            {
                [RotationInvariantMatchTool.PortInImage] = scene,
                [RotationInvariantMatchTool.PortTemplate] = tpl
            });

            using var result = tool.Execute(ctx, CancellationToken.None);
            result.Status.Should().Be(ToolStatus.Ok);
            result.GetOutput<double>(RotationInvariantMatchTool.PortBestScore).Should().BeGreaterThan(0.9);
        }
    }

    [Fact]
    public void ShapeMatchTool_RecognizesSameGeometry()
    {
        using var m1 = new Mat(100, 100, MatType.CV_8UC1, new Scalar(0));
        using var m2 = new Mat(100, 100, MatType.CV_8UC1, new Scalar(0));

        Cv2.Circle(m1, new Point(50, 50), 30, new Scalar(255), -1);
        Cv2.Circle(m2, new Point(50, 50), 20, new Scalar(255), -1); // Same shape (circle), different scale

        using var f1 = new ImageFrame(m1);
        using var f2 = new ImageFrame(m2);

        var tool = new ShapeMatchTool();
        var ctx = new ToolContext(new Dictionary<string, object?>
        {
            [ShapeMatchTool.PortInImage] = f1,
            [ShapeMatchTool.PortTemplate] = f2
        });

        using var result = tool.Execute(ctx, CancellationToken.None);
        result.Status.Should().Be(ToolStatus.Ok);
        var dist = result.GetOutput<double>(ShapeMatchTool.PortDisimilarity);
        dist.Should().BeLessThan(0.05); // Near zero for identical circles!
    }

    [Fact]
    public void PatternAlignTool_CalculatesDeltaOffsets()
    {
        var tool = new PatternAlignTool();
        tool.Parameters.First(p => p.Name == PatternAlignTool.ParamNominalX).Value = 100.0;
        tool.Parameters.First(p => p.Name == PatternAlignTool.ParamNominalY).Value = 100.0;

        var ctx = new ToolContext(new Dictionary<string, object?>
        {
            [PatternAlignTool.PortCurrentX] = 105.5,
            [PatternAlignTool.PortCurrentY] = 98.2,
            [PatternAlignTool.PortCurrentAngle] = 2.5
        });

        using var result = tool.Execute(ctx, CancellationToken.None);
        result.Status.Should().Be(ToolStatus.Ok);
        var dx = result.GetOutput<double>(PatternAlignTool.PortDeltaX);
        var dy = result.GetOutput<double>(PatternAlignTool.PortDeltaY);

        dx.Should().BeApproximately(5.5, 0.001);
        dy.Should().BeApproximately(-1.8, 0.001);
    }
}
