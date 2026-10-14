using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Tools;
using VisionStudio.Imaging.Identification;
using ZXing;

namespace VisionStudio.Imaging.Tests;

public class IdentificationToolsTests
{
    [Fact]
    public void BarcodeReaderTool_EncodesAndDecodesQrCodeAccurately()
    {
        // Generate QR code bitmap using ZXing BarcodeWriterPixelData
        const string payload = "VISION-STUDIO-PART-998";
        var writer = new BarcodeWriterPixelData
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new ZXing.QrCode.QrCodeEncodingOptions
            {
                Width = 200,
                Height = 200,
                Margin = 2
            }
        };

        var pixelData = writer.Write(payload);
        using var mat = new Mat(pixelData.Height, pixelData.Width, MatType.CV_8UC4);
        System.Runtime.InteropServices.Marshal.Copy(pixelData.Pixels, 0, mat.Data, pixelData.Pixels.Length);

        using var frame = new ImageFrame(mat);

        var tool = new BarcodeReaderTool();
        var ctx = new ToolContext(new Dictionary<string, object?> { [BarcodeReaderTool.PortInImage] = frame });

        using var result = tool.Execute(ctx, CancellationToken.None);
        result.Status.Should().Be(ToolStatus.Ok);
        var success = result.GetOutput<bool>(BarcodeReaderTool.PortSuccess);
        var text = result.GetOutput<string>(BarcodeReaderTool.PortText);

        success.Should().BeTrue();
        text.Should().Be(payload);
    }

    [Fact]
    public void ColorCheckTool_CalculatesDeltaEInRange()
    {
        using var mat = new Mat(50, 50, MatType.CV_8UC3, new Scalar(50, 50, 200)); // BGR: Reddish
        using var frame = new ImageFrame(mat);

        var tool = new ColorCheckTool();
        tool.Parameters.First(p => p.Name == ColorCheckTool.ParamRefR).Value = 200;
        tool.Parameters.First(p => p.Name == ColorCheckTool.ParamRefG).Value = 50;
        tool.Parameters.First(p => p.Name == ColorCheckTool.ParamRefB).Value = 50;
        tool.Parameters.First(p => p.Name == ColorCheckTool.ParamMaxDeltaE).Value = 10.0;

        var ctx = new ToolContext(new Dictionary<string, object?> { ["ImageIn"] = frame });
        using var result = tool.Execute(ctx, CancellationToken.None);

        result.Status.Should().Be(ToolStatus.Ok);
        var deltaE = result.GetOutput<double>(ColorCheckTool.PortDeltaE);
        var pass = result.GetOutput<bool>(ColorCheckTool.PortPass);

        deltaE.Should().BeLessThan(10.0);
        pass.Should().BeTrue();
    }

    [Fact]
    public void PresenceAbsenceTool_DetectsPresenceAccurately()
    {
        using var mat = new Mat(100, 100, MatType.CV_8UC1, new Scalar(0));
        // Add white blob (500 px)
        Cv2.Circle(mat, new Point(50, 50), 12, new Scalar(255), -1);
        using var frame = new ImageFrame(mat);

        var tool = new PresenceAbsenceTool();
        tool.Parameters.First(p => p.Name == PresenceAbsenceTool.ParamMinPixels).Value = 200;

        var ctx = new ToolContext(new Dictionary<string, object?> { [PresenceAbsenceTool.PortInImage] = frame });
        using var result = tool.Execute(ctx, CancellationToken.None);

        result.Status.Should().Be(ToolStatus.Ok);
        var isPresent = result.GetOutput<bool>(PresenceAbsenceTool.PortIsPresent);
        isPresent.Should().BeTrue();
    }
}
