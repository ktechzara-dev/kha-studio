using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Tools;
using VisionStudio.Imaging.Geometry;

namespace VisionStudio.Imaging.Tests;

public class GeometryToolsTests
{
    private static ImageFrame CreateSyntheticFrame(int width = 320, int height = 240)
    {
        var mat = new Mat(height, width, MatType.CV_8UC3, new Scalar(50, 50, 50));
        // Draw bright square at center
        Cv2.Rectangle(mat, new Rect(100, 80, 120, 80), new Scalar(200, 200, 200), -1);
        return new ImageFrame(mat, "synthetic", 1);
    }

    [Fact]
    public void CropTool_ExtractsSpecifiedWindow()
    {
        using var frame = CreateSyntheticFrame(400, 300);
        var tool = new CropTool();
        tool.Parameters.First(p => p.Name == CropTool.ParamX).Value = 50;
        tool.Parameters.First(p => p.Name == CropTool.ParamY).Value = 40;
        tool.Parameters.First(p => p.Name == CropTool.ParamWidth).Value = 200;
        tool.Parameters.First(p => p.Name == CropTool.ParamHeight).Value = 150;

        var ctx = new ToolContext(new Dictionary<string, object?> { [CropTool.PortInImage] = frame });
        using var result = tool.Execute(ctx, CancellationToken.None);

        result.Status.Should().Be(ToolStatus.Ok);
        var outFrame = result.GetImage(CropTool.PortOutImage);
        outFrame.Should().NotBeNull();
        outFrame!.Width.Should().Be(200);
        outFrame.Height.Should().Be(150);
        result.Overlays.Should().ContainSingle();
    }

    [Fact]
    public void RoiTool_GeneratesCircleMask()
    {
        using var frame = CreateSyntheticFrame(200, 200);
        var tool = new RoiTool();
        tool.Parameters.First(p => p.Name == RoiTool.ParamShape).Value = "Circle";
        tool.Parameters.First(p => p.Name == RoiTool.ParamCenterX).Value = 100;
        tool.Parameters.First(p => p.Name == RoiTool.ParamCenterY).Value = 100;
        tool.Parameters.First(p => p.Name == RoiTool.ParamRadius).Value = 40;

        var ctx = new ToolContext(new Dictionary<string, object?> { [RoiTool.PortInImage] = frame });
        using var result = tool.Execute(ctx, CancellationToken.None);

        result.Status.Should().Be(ToolStatus.Ok);
        var mask = result.GetImage(RoiTool.PortMask);
        mask.Should().NotBeNull();
        mask!.Mat.Get<byte>(100, 100).Should().Be(255); // Center inside circle
        mask.Mat.Get<byte>(10, 10).Should().Be(0);       // Corner outside circle
    }

    [Fact]
    public void RotateTool_RotatesImage()
    {
        using var frame = CreateSyntheticFrame(200, 200);
        var tool = new RotateTool();
        tool.Parameters.First(p => p.Name == RotateTool.ParamAngle).Value = 90.0;

        var ctx = new ToolContext(new Dictionary<string, object?> { [RotateTool.PortInImage] = frame });
        using var result = tool.Execute(ctx, CancellationToken.None);

        result.Status.Should().Be(ToolStatus.Ok);
        var outFrame = result.GetImage(RotateTool.PortOutImage);
        outFrame.Should().NotBeNull();
        outFrame!.Width.Should().Be(200);
        outFrame.Height.Should().Be(200);
    }

    [Fact]
    public void FlipTool_MirrorsHorizontal()
    {
        using var frame = CreateSyntheticFrame(100, 100);
        frame.Mat.Set(10, 10, new Vec3b(255, 0, 0)); // Marker on left side

        var tool = new FlipTool();
        tool.Parameters.First(p => p.Name == FlipTool.ParamMode).Value = "Horizontal";

        var ctx = new ToolContext(new Dictionary<string, object?> { [FlipTool.PortInImage] = frame });
        using var result = tool.Execute(ctx, CancellationToken.None);

        result.Status.Should().Be(ToolStatus.Ok);
        var outFrame = result.GetImage(FlipTool.PortOutImage);
        // After horizontal flip, left pixel (x=10) moved to right (x=100-1-10=89)
        var flippedPixel = outFrame!.Mat.Get<Vec3b>(10, 89);
        flippedPixel.Item0.Should().Be(255);
    }

    [Fact]
    public void ResizeTool_ScalesDimensions()
    {
        using var frame = CreateSyntheticFrame(400, 200);
        var tool = new ResizeTool();
        tool.Parameters.First(p => p.Name == ResizeTool.ParamMode).Value = "ExplicitSize";
        tool.Parameters.First(p => p.Name == ResizeTool.ParamWidth).Value = 200;
        tool.Parameters.First(p => p.Name == ResizeTool.ParamHeight).Value = 100;

        var ctx = new ToolContext(new Dictionary<string, object?> { [ResizeTool.PortInImage] = frame });
        using var result = tool.Execute(ctx, CancellationToken.None);

        result.Status.Should().Be(ToolStatus.Ok);
        var outFrame = result.GetImage(ResizeTool.PortOutImage);
        outFrame!.Width.Should().Be(200);
        outFrame.Height.Should().Be(100);
    }

    [Fact]
    public void PerspectiveWarpTool_WarpsQuadrilateral()
    {
        using var frame = CreateSyntheticFrame(400, 300);
        var tool = new PerspectiveWarpTool();
        tool.Parameters.First(p => p.Name == PerspectiveWarpTool.ParamWidth).Value = 300;
        tool.Parameters.First(p => p.Name == PerspectiveWarpTool.ParamHeight).Value = 200;

        var ctx = new ToolContext(new Dictionary<string, object?> { [PerspectiveWarpTool.PortInImage] = frame });
        using var result = tool.Execute(ctx, CancellationToken.None);

        result.Status.Should().Be(ToolStatus.Ok);
        var outFrame = result.GetImage(PerspectiveWarpTool.PortOutImage);
        outFrame!.Width.Should().Be(300);
        outFrame.Height.Should().Be(200);
    }

    [Fact]
    public void PolarUnwrapTool_UnwrapsAnnulus()
    {
        using var frame = CreateSyntheticFrame(200, 200);
        var tool = new PolarUnwrapTool();
        tool.Parameters.First(p => p.Name == PolarUnwrapTool.ParamMaxRadius).Value = 80.0;
        tool.Parameters.First(p => p.Name == PolarUnwrapTool.ParamWidth).Value = 360;
        tool.Parameters.First(p => p.Name == PolarUnwrapTool.ParamHeight).Value = 80;

        var ctx = new ToolContext(new Dictionary<string, object?> { [PolarUnwrapTool.PortInImage] = frame });
        using var result = tool.Execute(ctx, CancellationToken.None);

        result.Status.Should().Be(ToolStatus.Ok);
        var outFrame = result.GetImage(PolarUnwrapTool.PortOutImage);
        outFrame!.Width.Should().Be(360);
        outFrame.Height.Should().Be(80);
    }

    [Fact]
    public void MaskFromRegionTool_AppliesMask()
    {
        using var frame = CreateSyntheticFrame(100, 100);
        using var maskMat = new Mat(100, 100, MatType.CV_8UC1, new Scalar(0));
        Cv2.Rectangle(maskMat, new Rect(20, 20, 60, 60), new Scalar(255), -1);
        using var maskFrame = new ImageFrame(maskMat, "mask");

        var tool = new MaskFromRegionTool();
        var ctx = new ToolContext(new Dictionary<string, object?>
        {
            [MaskFromRegionTool.PortInImage] = frame,
            [MaskFromRegionTool.PortInMask] = maskFrame
        });

        using var result = tool.Execute(ctx, CancellationToken.None);
        result.Status.Should().Be(ToolStatus.Ok);
        var outFrame = result.GetImage(MaskFromRegionTool.PortOutImage);
        outFrame.Should().NotBeNull();
        // Area outside mask should be black (0)
        outFrame!.Mat.Get<Vec3b>(5, 5).Should().Be(new Vec3b(0, 0, 0));
    }

    [Fact]
    public void RotatedRoiTool_StraightensAndMasksImage()
    {
        using var frame = CreateSyntheticFrame(400, 300);
        var tool = new RotatedRoiTool();
        tool.Parameters.First(p => p.Name == RotatedRoiTool.ParamCenterX).Value = 200;
        tool.Parameters.First(p => p.Name == RotatedRoiTool.ParamCenterY).Value = 150;
        tool.Parameters.First(p => p.Name == RotatedRoiTool.ParamWidth).Value = 120;
        tool.Parameters.First(p => p.Name == RotatedRoiTool.ParamHeight).Value = 80;
        tool.Parameters.First(p => p.Name == RotatedRoiTool.ParamAngle).Value = 30.0;

        var ctx = new ToolContext(new Dictionary<string, object?> { [RotatedRoiTool.PortInImage] = frame });
        using var result = tool.Execute(ctx, CancellationToken.None);

        result.Status.Should().Be(ToolStatus.Ok);
        var straightened = result.GetImage(RotatedRoiTool.PortStraightenedImage);
        var mask = result.GetImage(RotatedRoiTool.PortMask);
        var masked = result.GetImage(RotatedRoiTool.PortMaskedImage);

        straightened.Should().NotBeNull();
        straightened!.Width.Should().Be(120);
        straightened.Height.Should().Be(80);

        mask.Should().NotBeNull();
        mask!.Width.Should().Be(400);
        mask.Height.Should().Be(300);

        masked.Should().NotBeNull();
        masked!.Width.Should().Be(400);
    }

    [Fact]
    public void PolygonRoiTool_GeneratesPolygonMask()
    {
        using var frame = CreateSyntheticFrame(400, 300);
        var tool = new PolygonRoiTool();
        tool.Parameters.First(p => p.Name == PolygonRoiTool.ParamPoints).Value = "100,50; 300,50; 350,250; 80,220";

        var ctx = new ToolContext(new Dictionary<string, object?> { [PolygonRoiTool.PortInImage] = frame });
        using var result = tool.Execute(ctx, CancellationToken.None);

        result.Status.Should().Be(ToolStatus.Ok);
        var mask = result.GetImage(PolygonRoiTool.PortMask);
        mask.Should().NotBeNull();
        mask!.Mat.Get<byte>(150, 200).Should().Be(255); // Interior should be white
        mask.Mat.Get<byte>(10, 10).Should().Be(0); // Exterior should be black

        var area = result.Outputs[PolygonRoiTool.PortArea];
        area.Should().NotBeNull();
        ((double)area!).Should().BeGreaterThan(0);
    }

    [Fact]
    public void CircularRoiTool_GeneratesAnnulusMaskAndCrops()
    {
        using var frame = CreateSyntheticFrame(400, 300);
        var tool = new CircularRoiTool();
        tool.Parameters.First(p => p.Name == CircularRoiTool.ParamCenterX).Value = 200;
        tool.Parameters.First(p => p.Name == CircularRoiTool.ParamCenterY).Value = 150;
        tool.Parameters.First(p => p.Name == CircularRoiTool.ParamOuterRadius).Value = 60;
        tool.Parameters.First(p => p.Name == CircularRoiTool.ParamInnerRadius).Value = 20;

        var ctx = new ToolContext(new Dictionary<string, object?> { [CircularRoiTool.PortInImage] = frame });
        using var result = tool.Execute(ctx, CancellationToken.None);

        result.Status.Should().Be(ToolStatus.Ok);
        var mask = result.GetImage(CircularRoiTool.PortMask);
        var cropped = result.GetImage(CircularRoiTool.PortCroppedImage);

        mask.Should().NotBeNull();
        mask!.Mat.Get<byte>(150, 200).Should().Be(0); // Center hole is 0
        mask.Mat.Get<byte>(150, 240).Should().Be(255); // Ring band is 255

        cropped.Should().NotBeNull();
        cropped!.Width.Should().Be(120);
        cropped.Height.Should().Be(120);
    }
}

