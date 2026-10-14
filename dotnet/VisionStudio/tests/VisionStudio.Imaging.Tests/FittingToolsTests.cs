using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Tools;
using VisionStudio.Imaging.Fitting;

namespace VisionStudio.Imaging.Tests;

public class FittingToolsTests
{
    [Fact]
    public void GeometryFitTool_FitsCircleModel()
    {
        using var mat = new Mat(100, 100, MatType.CV_8UC1, new Scalar(0));
        Cv2.Circle(mat, new Point(50, 50), 30, new Scalar(255), -1);
        using var frame = new ImageFrame(mat);

        var tool = new GeometryFitTool();
        tool.Parameters.First(p => p.Name == GeometryFitTool.ParamGeometry).Value = "Circle";

        var ctx = new ToolContext(new Dictionary<string, object?> { [GeometryFitTool.PortInImage] = frame });
        using var result = tool.Execute(ctx, CancellationToken.None);

        result.Status.Should().Be(ToolStatus.Ok);
        var cx = result.GetOutput<double>(GeometryFitTool.PortCenterX);
        var cy = result.GetOutput<double>(GeometryFitTool.PortCenterY);
        var radius = result.GetOutput<double>(GeometryFitTool.PortDimension);

        cx.Should().BeInRange(48.0, 52.0);
        cy.Should().BeInRange(48.0, 52.0);
        radius.Should().BeInRange(28.0, 32.0);
    }

    [Fact]
    public void HoughLinesTool_DetectsLines()
    {
        using var mat = new Mat(100, 100, MatType.CV_8UC1, new Scalar(0));
        Cv2.Line(mat, new Point(10, 50), new Point(90, 50), new Scalar(255), 2);
        using var frame = new ImageFrame(mat);

        var tool = new HoughLinesTool();
        var ctx = new ToolContext(new Dictionary<string, object?> { [HoughLinesTool.PortInImage] = frame });

        using var result = tool.Execute(ctx, CancellationToken.None);
        result.Status.Should().Be(ToolStatus.Ok);
        result.GetOutput<int>(HoughLinesTool.PortLineCount).Should().BeGreaterThan(0);
    }

    [Fact]
    public void HoughCirclesTool_DetectsCircles()
    {
        using var mat = new Mat(100, 100, MatType.CV_8UC1, new Scalar(0));
        Cv2.Circle(mat, new Point(50, 50), 25, new Scalar(255), 2);
        using var frame = new ImageFrame(mat);

        var tool = new HoughCirclesTool();
        tool.Parameters.First(p => p.Name == HoughCirclesTool.ParamMinR).Value = 20;
        tool.Parameters.First(p => p.Name == HoughCirclesTool.ParamMaxR).Value = 30;

        var ctx = new ToolContext(new Dictionary<string, object?> { [HoughCirclesTool.PortInImage] = frame });
        using var result = tool.Execute(ctx, CancellationToken.None);

        result.Status.Should().Be(ToolStatus.Ok);
        result.GetOutput<int>(HoughCirclesTool.PortCircleCount).Should().BeGreaterThan(0);
    }

    [Fact]
    public void GeometricDistanceTool_CalculatesPointToPointDistance()
    {
        var tool = new GeometricDistanceTool();
        tool.Parameters.First(p => p.Name == GeometricDistanceTool.ParamMode).Value = "PointToPoint";
        tool.Parameters.First(p => p.Name == GeometricDistanceTool.ParamX1).Value = 0.0;
        tool.Parameters.First(p => p.Name == GeometricDistanceTool.ParamY1).Value = 0.0;
        tool.Parameters.First(p => p.Name == GeometricDistanceTool.ParamX2).Value = 30.0;
        tool.Parameters.First(p => p.Name == GeometricDistanceTool.ParamY2).Value = 40.0;

        var ctx = new ToolContext(new Dictionary<string, object?>());
        using var result = tool.Execute(ctx, CancellationToken.None);

        result.Status.Should().Be(ToolStatus.Ok);
        var dist = result.GetOutput<double>(GeometricDistanceTool.PortDistance);
        dist.Should().Be(50.0); // 3-4-5 triangle
    }
}
