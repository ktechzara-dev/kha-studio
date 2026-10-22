using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Tools;
using VisionStudio.Imaging.Identification;
using VisionStudio.Imaging.Grading;
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
    public void BarcodeWriter_CanGenerateAllThreeBarcodes()
    {
        var barWriter = new BarcodeWriterPixelData
        {
            Format = BarcodeFormat.CODE_128,
            Options = new ZXing.Common.EncodingOptions { Width = 480, Height = 80, Margin = 2 }
        };
        var barPixels = barWriter.Write("KHA-PART-2026-8902");
        barPixels.Should().NotBeNull();

        var qrWriter = new BarcodeWriterPixelData
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new ZXing.QrCode.QrCodeEncodingOptions { Width = 180, Height = 180, Margin = 1 }
        };
        var qrPixels = qrWriter.Write("KHA-STUDIO-VISION-PART-8902");
        qrPixels.Should().NotBeNull();

        var dmWriter = new BarcodeWriterPixelData
        {
            Format = BarcodeFormat.DATA_MATRIX,
            Options = new ZXing.Datamatrix.DatamatrixEncodingOptions { Width = 180, Height = 180, Margin = 1 }
        };
        var dmPixels = dmWriter.Write("KHA-VS-8902-X-C03");
        dmPixels.Should().NotBeNull();

        // Composite canvas 800x600
        using var canvas = new Mat(600, 800, MatType.CV_8UC1, new Scalar(255));
        
        // Copy Code 128 (barPixels)
        using var barMat = new Mat(barPixels.Height, barPixels.Width, MatType.CV_8UC4);
        System.Runtime.InteropServices.Marshal.Copy(barPixels.Pixels, 0, barMat.Data, barPixels.Pixels.Length);
        using var barGray = new Mat();
        Cv2.CvtColor(barMat, barGray, ColorConversionCodes.BGRA2GRAY);
        barGray.CopyTo(new Mat(canvas, new Rect(110, 160, barGray.Width, barGray.Height)));

        // Copy QR Code
        using var qrMat = new Mat(qrPixels.Height, qrPixels.Width, MatType.CV_8UC4);
        System.Runtime.InteropServices.Marshal.Copy(qrPixels.Pixels, 0, qrMat.Data, qrPixels.Pixels.Length);
        using var qrGray = new Mat();
        Cv2.CvtColor(qrMat, qrGray, ColorConversionCodes.BGRA2GRAY);
        qrGray.CopyTo(new Mat(canvas, new Rect(120, 310, qrGray.Width, qrGray.Height)));

        // Test BarcodeReaderTool on canvas
        using var frame = new ImageFrame(canvas);
        var tool = new BarcodeReaderTool();
        var ctx = new ToolContext(new Dictionary<string, object?> { [BarcodeReaderTool.PortInImage] = frame });
        using var res = tool.Execute(ctx, CancellationToken.None);
        
        var isOk = res.GetOutput<bool>(BarcodeReaderTool.PortSuccess);
        var decoded = res.GetOutput<string>(BarcodeReaderTool.PortText);
        var format = res.GetOutput<string>(BarcodeReaderTool.PortFormat);
        
        isOk.Should().BeTrue();

        // Test CodeGradingTool on same canvas
        var gradeTool = new CodeGradingTool();
        var gradeCtx = new ToolContext(new Dictionary<string, object?> { [CodeGradingTool.PortInImage] = frame });
        using var gradeRes = gradeTool.Execute(gradeCtx, CancellationToken.None);
        var letter = gradeRes.GetOutput<string>(CodeGradingTool.PortLetterGrade);
        var decodedGrade = gradeRes.GetOutput<string>(CodeGradingTool.PortDecodedText);
        var numGrade = gradeRes.GetOutput<double>(CodeGradingTool.PortNumericGrade);

        decodedGrade.Should().NotBeNullOrEmpty();
        numGrade.Should().BeGreaterThan(0.0);
    }

    [Fact]
    public void GenerateRealisticSampleIndustrialCodesLabel()
    {
        using var canvas = new Mat(600, 800, MatType.CV_8UC3, new Scalar(24, 28, 36));

        // White vinyl label card
        var cardRect = new Rect(60, 35, 680, 530);
        Cv2.Rectangle(canvas, cardRect, new Scalar(250, 252, 255), -1);
        Cv2.Rectangle(canvas, cardRect, new Scalar(190, 205, 225), 2);

        // Header
        Cv2.PutText(canvas, "INDUSTRIAL LOGISTICS & TRACEABILITY LABEL", new Point(90, 75), HersheyFonts.HersheySimplex, 0.65, new Scalar(15, 23, 42), 2, LineTypes.AntiAlias);
        Cv2.PutText(canvas, "PART NO: KHA-VS-8902-X | REVISION: C03 | PASS ISO 15415/15416", new Point(90, 102), HersheyFonts.HersheySimplex, 0.42, new Scalar(71, 85, 105), 1, LineTypes.AntiAlias);
        Cv2.Line(canvas, new Point(90, 115), new Point(710, 115), new Scalar(210, 220, 235), 2);

        // 1. Code 128
        var barWriter = new BarcodeWriterPixelData
        {
            Format = BarcodeFormat.CODE_128,
            Options = new ZXing.Common.EncodingOptions { Width = 520, Height = 85, Margin = 2, PureBarcode = false }
        };
        var barPix = barWriter.Write("KHA-PART-2026-8902");
        using var barMat = new Mat(barPix.Height, barPix.Width, MatType.CV_8UC4);
        System.Runtime.InteropServices.Marshal.Copy(barPix.Pixels, 0, barMat.Data, barPix.Pixels.Length);
        using var barBgr = new Mat();
        Cv2.CvtColor(barMat, barBgr, ColorConversionCodes.BGRA2BGR);
        barBgr.CopyTo(new Mat(canvas, new Rect(140, 135, barBgr.Width, barBgr.Height)));
        Cv2.PutText(canvas, "* KHA-PART-2026-8902 *", new Point(285, 242), HersheyFonts.HersheySimplex, 0.52, new Scalar(15, 23, 42), 1, LineTypes.AntiAlias);

        // 2. QR Code
        var qrWriter = new BarcodeWriterPixelData
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new ZXing.QrCode.QrCodeEncodingOptions { Width = 175, Height = 175, Margin = 1 }
        };
        var qrPix = qrWriter.Write("https://khastudio.vn/part/KHA-VS-8902-X");
        using var qrMat = new Mat(qrPix.Height, qrPix.Width, MatType.CV_8UC4);
        System.Runtime.InteropServices.Marshal.Copy(qrPix.Pixels, 0, qrMat.Data, qrPix.Pixels.Length);
        using var qrBgr = new Mat();
        Cv2.CvtColor(qrMat, qrBgr, ColorConversionCodes.BGRA2BGR);
        qrBgr.CopyTo(new Mat(canvas, new Rect(115, 275, qrBgr.Width, qrBgr.Height)));
        Cv2.PutText(canvas, "QR Code (ISO/IEC 18004)", new Point(115, 475), HersheyFonts.HersheySimplex, 0.42, new Scalar(30, 41, 59), 1, LineTypes.AntiAlias);

        // 3. DataMatrix
        var dmWriter = new BarcodeWriterPixelData
        {
            Format = BarcodeFormat.DATA_MATRIX,
            Options = new ZXing.Datamatrix.DatamatrixEncodingOptions { Width = 175, Height = 175, Margin = 1 }
        };
        var dmPix = dmWriter.Write("KHA-VS-8902-X");
        using var dmMat = new Mat(dmPix.Height, dmPix.Width, MatType.CV_8UC4);
        System.Runtime.InteropServices.Marshal.Copy(dmPix.Pixels, 0, dmMat.Data, dmPix.Pixels.Length);
        using var dmBgr = new Mat();
        Cv2.CvtColor(dmMat, dmBgr, ColorConversionCodes.BGRA2BGR);
        dmBgr.CopyTo(new Mat(canvas, new Rect(510, 275, dmBgr.Width, dmBgr.Height)));
        Cv2.PutText(canvas, "DataMatrix (ISO/IEC 16022)", new Point(495, 475), HersheyFonts.HersheySimplex, 0.42, new Scalar(30, 41, 59), 1, LineTypes.AntiAlias);

        // Save to target paths
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(dir) && !File.Exists(Path.Combine(dir, "VisionStudio.sln")))
        {
            dir = Path.GetDirectoryName(dir);
        }
        var root = dir ?? @"d:\BUITAPP\kha-studio-app\pallet-studio-app\dotnet\VisionStudio";

        var paths = new[]
        {
            Path.Combine(root, @"samples\images\02_industrial_codes_label.png"),
            Path.Combine(root, @"src\VisionStudio.App\bin\Debug\net8.0-windows\samples\images\02_industrial_codes_label.png"),
            Path.Combine(root, @"tests\VisionStudio.App.Tests\bin\Debug\net8.0-windows\samples\images\02_industrial_codes_label.png")
        };

        foreach (var p in paths)
        {
            var pDir = Path.GetDirectoryName(p);
            if (pDir != null) Directory.CreateDirectory(pDir);
            Cv2.ImWrite(p, canvas);
        }
    }

    [Fact]
    public void TestIndustrialCodesLabel_DecodesSuccessfully()
    {
        var samplePath = @"d:\BUITAPP\kha-studio-app\pallet-studio-app\dotnet\VisionStudio\samples\images\02_industrial_codes_label.png";
        File.Exists(samplePath).Should().BeTrue();
        using var mat = Cv2.ImRead(samplePath);
        mat.Empty().Should().BeFalse();
        using var frame = new ImageFrame(mat);

        var tool = new BarcodeReaderTool();
        var ctx = new ToolContext(new Dictionary<string, object?> { [BarcodeReaderTool.PortInImage] = frame });
        using var res = tool.Execute(ctx, CancellationToken.None);

        var isSuccess = res.GetOutput<bool>(BarcodeReaderTool.PortSuccess);
        var text = res.GetOutput<string>(BarcodeReaderTool.PortText);
        var format = res.GetOutput<string>(BarcodeReaderTool.PortFormat);

        isSuccess.Should().BeTrue();
        text.Should().NotBeNullOrEmpty();

        var gradeTool = new CodeGradingTool();
        var gradeCtx = new ToolContext(new Dictionary<string, object?> { [CodeGradingTool.PortInImage] = frame });
        using var gradeRes = gradeTool.Execute(gradeCtx, CancellationToken.None);

        var gradePass = gradeRes.GetOutput<bool>(CodeGradingTool.PortPassGrade);
        var gradeLetter = gradeRes.GetOutput<string>(CodeGradingTool.PortLetterGrade);
        var gradeNum = gradeRes.GetOutput<double>(CodeGradingTool.PortNumericGrade);
        var gradeText = gradeRes.GetOutput<string>(CodeGradingTool.PortDecodedText);

        gradeText.Should().NotBeNullOrEmpty();
        gradeNum.Should().BeGreaterThan(0.0);
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
