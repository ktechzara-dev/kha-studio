using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Tools;
using VisionStudio.Imaging.Acquisition;

namespace VisionStudio.Imaging.Tests;

public class AcquisitionToolsTests
{
    [Fact]
    public void CameraGrabTool_GeneratesSimulatedTargetPattern()
    {
        var tool = new CameraGrabTool();
        tool.Parameters.First(p => p.Name == CameraGrabTool.ParamWidth).Value = 400;
        tool.Parameters.First(p => p.Name == CameraGrabTool.ParamHeight).Value = 300;
        tool.Parameters.First(p => p.Name == CameraGrabTool.ParamPattern).Value = "TargetPart";

        var ctx = new ToolContext(new Dictionary<string, object?>());
        using var result = tool.Execute(ctx, CancellationToken.None);

        result.Status.Should().Be(ToolStatus.Ok);
        var frame = result.GetImage(CameraGrabTool.PortImage);
        frame.Should().NotBeNull();
        frame!.Width.Should().Be(400);
        frame!.Height.Should().Be(300);
        frame.Channels.Should().Be(3);
        result.GetOutput<long>(CameraGrabTool.PortFrameId).Should().Be(1);
    }

    [Fact]
    public void ImageFileTool_ReadsSavedSyntheticImage()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"vs_test_{Guid.NewGuid():N}.png");
        try
        {
            using (var testMat = new Mat(120, 160, MatType.CV_8UC3, new Scalar(0, 128, 255)))
            {
                Cv2.ImWrite(tempFile, testMat);
            }

            var tool = new ImageFileTool();
            tool.Parameters.First(p => p.Name == ImageFileTool.ParamFilePath).Value = tempFile;

            var ctx = new ToolContext(new Dictionary<string, object?>());
            using var result = tool.Execute(ctx, CancellationToken.None);

            result.Status.Should().Be(ToolStatus.Ok);
            var frame = result.GetImage(ImageFileTool.PortImage);
            frame.Should().NotBeNull();
            frame!.Width.Should().Be(160);
            frame!.Height.Should().Be(120);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void VideoFileTool_SeekTest()
    {
        var videoPath = @"C:\Users\KHA\Desktop\1792125768167_7096571893584373167_g5467388982485979187.mp4";
        if (!File.Exists(videoPath)) return;
        
        var tool = new VideoFileTool();
        tool.Parameters.First(p => p.Name == VideoFileTool.ParamFilePath).Value = videoPath;
        var ctx = new ToolContext(new Dictionary<string, object?>());

        using var res = tool.Execute(ctx, CancellationToken.None);
        res.Status.Should().Be(ToolStatus.Ok);

        tool.Seek(15);
        using var resSeek = tool.Execute(ctx, CancellationToken.None);
        resSeek.Status.Should().Be(ToolStatus.Ok);
        tool.CurrentFrame.Should().Be(16);
    }
}
