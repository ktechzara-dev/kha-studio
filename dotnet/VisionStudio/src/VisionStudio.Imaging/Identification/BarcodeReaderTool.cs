using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Overlays;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;
using ZXing;

namespace VisionStudio.Imaging.Identification;

/// <summary>
/// High-speed LuminanceSource adapter bridging OpenCvSharp grayscale Mat buffers into ZXing.Net.
/// </summary>
public sealed class MatLuminanceSource : BaseLuminanceSource
{
    private readonly byte[] _luminances;

    public MatLuminanceSource(Mat grayMat) : base(grayMat.Cols, grayMat.Rows)
    {
        if (grayMat.Type() != MatType.CV_8UC1)
        {
            throw new ArgumentException("MatLuminanceSource requires a CV_8UC1 grayscale Mat.", nameof(grayMat));
        }

        var totalBytes = grayMat.Rows * grayMat.Cols;
        _luminances = new byte[totalBytes];
        System.Runtime.InteropServices.Marshal.Copy(grayMat.Data, _luminances, 0, totalBytes);
    }

    public override byte[] Matrix => _luminances;

    public override byte[] getRow(int y, byte[]? row)
    {
        if (y < 0 || y >= Height) throw new ArgumentOutOfRangeException(nameof(y));
        var width = Width;
        if (row == null || row.Length < width) row = new byte[width];
        Array.Copy(_luminances, y * width, row, 0, width);
        return row;
    }

    protected override LuminanceSource CreateLuminanceSource(byte[] newLuminances, int width, int height)
    {
        return new MatLuminanceSourceDirect(newLuminances, width, height);
    }

    private sealed class MatLuminanceSourceDirect : BaseLuminanceSource
    {
        private readonly byte[] _data;
        public MatLuminanceSourceDirect(byte[] data, int w, int h) : base(w, h) => _data = data;
        public override byte[] Matrix => _data;
        public override byte[] getRow(int y, byte[]? row)
        {
            var width = Width;
            if (row == null || row.Length < width) row = new byte[width];
            Array.Copy(_data, y * width, row, 0, width);
            return row;
        }
        protected override LuminanceSource CreateLuminanceSource(byte[] newLuminances, int width, int height)
            => new MatLuminanceSourceDirect(newLuminances, width, height);
    }
}

/// <summary>
/// Industrial 1D/2D optical code reader for QR Codes, DataMatrix, Code 128, EAN, and Code 39 via ZXing.Net.
/// </summary>
[VisionTool("Identification.BarcodeReader", "Barcode / 2D Code Reader", "Identification", Description = "Decodes 1D (Code128, EAN) and 2D (QR, DataMatrix) optical codes")]
public sealed class BarcodeReaderTool : VisionToolBase
{
    public const string ParamFormat = "CodeType";

    public const string PortInImage = "ImageIn";
    public const string PortText = "DecodedText";
    public const string PortFormat = "DetectedFormat";
    public const string PortSuccess = "IsSuccess";
    public const string PortAnnotatedImage = "AnnotatedImage";

    private readonly ToolParameter _pFormat;

    public BarcodeReaderTool() : base("Identification.BarcodeReader", "Barcode / 2D Code Reader", "Identification", "Optical barcode and QR reader")
    {
        _pFormat = AddParameter(new ToolParameter(
            ParamFormat, "Code Family", ParameterType.Enum, "Auto",
            enumOptions: new[] { "Auto", "QR_CODE", "DATA_MATRIX", "CODE_128", "EAN_13", "CODE_39" }));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));

        AddOutput(new ToolPort(PortText, "Decoded Text", PortType.String, PortDirection.Output));
        AddOutput(new ToolPort(PortFormat, "Detected Format", PortType.String, PortDirection.Output));
        AddOutput(new ToolPort(PortSuccess, "Is Success", PortType.Boolean, PortDirection.Output));
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

        var lumSource = new MatLuminanceSource(gray);
        var reader = new BarcodeReaderGeneric
        {
            AutoRotate = true,
            Options = new ZXing.Common.DecodingOptions
            {
                TryHarder = true,
                PureBarcode = false
            }
        };

        var formatStr = _pFormat.GetValue<string>() ?? "Auto";
        if (Enum.TryParse<BarcodeFormat>(formatStr, ignoreCase: true, out var specificFormat))
        {
            reader.Options.PossibleFormats = new[] { specificFormat };
        }

        var decoded = reader.Decode(lumSource);

        var annotated = src.Clone();
        var overlays = new List<IOverlayPrimitive>();

        var isFound = decoded != null;
        var text = decoded?.Text ?? string.Empty;
        var foundFormat = decoded?.BarcodeFormat.ToString() ?? string.Empty;

        if (isFound && decoded?.ResultPoints != null && decoded.ResultPoints.Length > 0)
        {
            var pts = decoded.ResultPoints.Select(p => new Point2f(p.X, p.Y)).ToList();
            if (pts.Count >= 2)
            {
                var poly = new PolygonOverlay(pts, new Scalar(0, 255, 0), Thickness: 2);
                overlays.Add(poly);
                poly.Render(annotated);
            }

            var textPos = pts[0];
            var textOv = new TextOverlay($"[{foundFormat}] {text}", new Point2f(textPos.X, Math.Max(20, textPos.Y - 10)), new Scalar(0, 255, 0), FontScale: 0.6, Thickness: 2);
            overlays.Add(textOv);
            textOv.Render(annotated);
        }

        var outFrame = new ImageFrame(annotated, inputFrame.CameraId, inputFrame.FrameId, inputFrame.Format);
        var outputs = new Dictionary<string, object?>
        {
            [PortText] = text,
            [PortFormat] = foundFormat,
            [PortSuccess] = isFound,
            [PortAnnotatedImage] = outFrame
        };

        return ToolResult.Ok(outputs, overlays, message: isFound ? $"Decoded [{foundFormat}]: '{text}'" : "No barcode/QR detected");
    }
}
