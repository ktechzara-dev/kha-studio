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
        if (grayMat == null || grayMat.Empty() || grayMat.Data == IntPtr.Zero)
        {
            throw new ArgumentException("MatLuminanceSource requires a non-empty CV_8UC1 grayscale Mat.", nameof(grayMat));
        }

        if (grayMat.Type() != MatType.CV_8UC1)
        {
            throw new ArgumentException("MatLuminanceSource requires a CV_8UC1 grayscale Mat.", nameof(grayMat));
        }

        var totalBytes = grayMat.Rows * grayMat.Cols;
        _luminances = new byte[totalBytes];

        if (!grayMat.IsContinuous())
        {
            using var continuous = grayMat.Clone();
            System.Runtime.InteropServices.Marshal.Copy(continuous.Data, _luminances, 0, totalBytes);
        }
        else
        {
            System.Runtime.InteropServices.Marshal.Copy(grayMat.Data, _luminances, 0, totalBytes);
        }
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
    public const string ParamRoiX = "RoiX";
    public const string ParamRoiY = "RoiY";
    public const string ParamRoiWidth = "RoiWidth";
    public const string ParamRoiHeight = "RoiHeight";

    public const string PortInImage = "ImageIn";
    public const string PortImageOut = "ImageOut";
    public const string PortText = "DecodedText";
    public const string PortFormat = "DetectedFormat";
    public const string PortSuccess = "IsSuccess";
    public const string PortAnnotatedImage = "AnnotatedImage";

    private readonly ToolParameter _pFormat;
    private readonly ToolParameter _pRoiX;
    private readonly ToolParameter _pRoiY;
    private readonly ToolParameter _pRoiW;
    private readonly ToolParameter _pRoiH;

    public BarcodeReaderTool() : base("Identification.BarcodeReader", "Barcode / 2D Code Reader", "Identification", "Optical barcode and QR reader")
    {
        _pFormat = AddParameter(new ToolParameter(
            ParamFormat, "Code Family", ParameterType.Enum, "Auto",
            enumOptions: new[] { "Auto", "QR_CODE", "DATA_MATRIX", "CODE_128", "EAN_13", "CODE_39" }));

        _pRoiX = AddParameter(new ToolParameter(ParamRoiX, "ROI X", ParameterType.Double, 0.0, min: 0.0, max: 10000.0, unit: "px"));
        _pRoiY = AddParameter(new ToolParameter(ParamRoiY, "ROI Y", ParameterType.Double, 0.0, min: 0.0, max: 10000.0, unit: "px"));
        _pRoiW = AddParameter(new ToolParameter(ParamRoiWidth, "ROI Width", ParameterType.Double, 0.0, min: 0.0, max: 10000.0, unit: "px"));
        _pRoiH = AddParameter(new ToolParameter(ParamRoiHeight, "ROI Height", ParameterType.Double, 0.0, min: 0.0, max: 10000.0, unit: "px"));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));

        AddOutput(new ToolPort(PortText, "Decoded Text", PortType.String, PortDirection.Output));
        AddOutput(new ToolPort(PortFormat, "Detected Format", PortType.String, PortDirection.Output));
        AddOutput(new ToolPort(PortSuccess, "Is Success", PortType.Boolean, PortDirection.Output));
        AddOutput(new ToolPort(PortImageOut, "Clean Image Out", PortType.Image, PortDirection.Output));
        AddOutput(new ToolPort(PortAnnotatedImage, "Annotated Image", PortType.Image, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var inputFrame = ctx.GetImage(PortInImage);
        if (inputFrame == null || inputFrame.Mat == null || inputFrame.Mat.Empty())
            return ToolResult.Failed("Missing or empty input image.");

        var src = inputFrame.Mat;
        int imgW = src.Width;
        int imgH = src.Height;

        double rx = _pRoiX.GetValue<double>();
        double ry = _pRoiY.GetValue<double>();
        double rw = _pRoiW.GetValue<double>();
        double rh = _pRoiH.GetValue<double>();

        bool useRoi = rw > 5 && rh > 5;
        Rect scanRect = new Rect(0, 0, imgW, imgH);
        if (useRoi)
        {
            int cx = Math.Clamp((int)rx, 0, imgW - 1);
            int cy = Math.Clamp((int)ry, 0, imgH - 1);
            int cw = Math.Clamp((int)rw, 1, imgW - cx);
            int ch = Math.Clamp((int)rh, 1, imgH - cy);
            scanRect = new Rect(cx, cy, cw, ch);
        }

        using var scanMat = new Mat(src, scanRect);
        using var gray = new Mat();
        if (scanMat.Channels() > 1)
        {
            Cv2.CvtColor(scanMat, gray, ColorConversionCodes.BGR2GRAY);
        }
        else
        {
            scanMat.CopyTo(gray);
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

        var overlays = new List<IOverlayPrimitive>();

        if (useRoi)
        {
            overlays.Add(new RectOverlay(new Rect2f(scanRect.X, scanRect.Y, scanRect.Width, scanRect.Height), new Scalar(255, 200, 0), Thickness: 1));
        }

        var resultsList = new List<Result>();
        try
        {
            var multi = reader.DecodeMultiple(lumSource);
            if (multi != null && multi.Length > 0)
            {
                resultsList.AddRange(multi);
            }
        }
        catch { }

        if (resultsList.Count == 0)
        {
            try
            {
                var single = reader.Decode(lumSource);
                if (single != null) resultsList.Add(single);
            }
            catch { }
        }

        var annotated = src.Clone();
        bool isFound = resultsList.Count > 0;
        string primaryText = isFound ? resultsList[0].Text : string.Empty;
        string primaryFormat = isFound ? resultsList[0].BarcodeFormat.ToString() : string.Empty;

        if (isFound)
        {
            foreach (var res in resultsList)
            {
                var format = res.BarcodeFormat.ToString();
                var text = res.Text;

                if (res.ResultPoints != null && res.ResultPoints.Length >= 2)
                {
                    var pts = res.ResultPoints.Select(p => new Point2f(p.X + scanRect.X, p.Y + scanRect.Y)).ToList();
                    var poly = new PolygonOverlay(pts, new Scalar(0, 255, 100), Thickness: 3);
                    overlays.Add(poly);
                    poly.Render(annotated);

                    float minX = pts.Min(p => p.X);
                    float minY = pts.Min(p => p.Y);
                    var textOv = new TextOverlay($"🏷️ [{format}] {text}", new Point2f(minX, Math.Max(20, minY - 8)), new Scalar(0, 255, 100), FontScale: 0.55, Thickness: 2);
                    overlays.Add(textOv);
                    textOv.Render(annotated);
                }
                else
                {
                    var banner = new TextOverlay($"🏷️ [{format}] {text}", new Point2f(scanRect.X + 15, Math.Max(25, scanRect.Y + 25)), new Scalar(0, 255, 100), FontScale: 0.6, Thickness: 2);
                    overlays.Add(banner);
                    banner.Render(annotated);
                }
            }

            overlays.Add(new TextOverlay($"✅ Đã đọc {resultsList.Count} mã: {primaryText}", new Point2f(10, imgH - 15), new Scalar(0, 255, 100), FontScale: 0.55, Thickness: 2));
        }
        else
        {
            overlays.Add(new TextOverlay("⚠️ Không tìm thấy mã vạch / QR (No Code Detected)", new Point2f(10, 25), new Scalar(0, 50, 255), FontScale: 0.55, Thickness: 2));
        }

        var outFrame = new ImageFrame(annotated, inputFrame.CameraId, inputFrame.FrameId, inputFrame.Format);
        var cleanFrame = inputFrame.Clone();

        var outputs = new Dictionary<string, object?>
        {
            [PortText] = isFound ? (resultsList.Count == 1 ? primaryText : string.Join(" | ", resultsList.Select(r => r.Text))) : string.Empty,
            [PortFormat] = isFound ? (resultsList.Count == 1 ? primaryFormat : string.Join(", ", resultsList.Select(r => r.BarcodeFormat.ToString()).Distinct())) : string.Empty,
            [PortSuccess] = isFound,
            [PortImageOut] = cleanFrame,
            [PortAnnotatedImage] = outFrame
        };

        return ToolResult.Ok(outputs, overlays, message: isFound ? $"Đã đọc [{primaryFormat}]: '{primaryText}'" : "Không tìm thấy mã vạch / QR");
    }
}
