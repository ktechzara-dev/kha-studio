using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Overlays;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;
using VisionStudio.Imaging.Identification;
using ZXing;

namespace VisionStudio.Imaging.Grading;

/// <summary>
/// Code Quality Grading Tool conforming to ISO/IEC 15415 (2D) and ISO/IEC 15416 (1D) print quality inspection.
/// </summary>
[VisionTool("Grading.CodeQuality", "Code Quality Grading (ISO/IEC)", "Identification", Description = "Evaluates print quality grades (A-F) for 1D and 2D barcodes per ISO/IEC 15415/15416")]
public sealed class CodeGradingTool : VisionToolBase
{
    public const string ParamMinAcceptableGrade = "MinAcceptableGrade";
    public const string ParamRoiX = "RoiX";
    public const string ParamRoiY = "RoiY";
    public const string ParamRoiWidth = "RoiWidth";
    public const string ParamRoiHeight = "RoiHeight";

    public const string PortInImage = "ImageIn";
    public const string PortInDecodedText = "DecodedTextIn";
    public const string PortDecodedText = "DecodedText";
    public const string PortLetterGrade = "OverallGrade";
    public const string PortNumericGrade = "NumericGrade";
    public const string PortContrastGrade = "SymbolContrastGrade";
    public const string PortAnuGrade = "AxialNonUniformityGrade";
    public const string PortModGrade = "ModulationGrade";
    public const string PortPassGrade = "PassGrade";
    public const string PortOutImage = "AnnotatedImage";

    private readonly ToolParameter _pMinGrade;
    private readonly ToolParameter _pRoiX;
    private readonly ToolParameter _pRoiY;
    private readonly ToolParameter _pRoiW;
    private readonly ToolParameter _pRoiH;

    public CodeGradingTool() : base("Grading.CodeQuality", "Code Quality Grading (ISO/IEC)", "Identification", "Evaluates print quality grades (A-F) for 1D and 2D barcodes per ISO/IEC 15415/15416")
    {
        _pMinGrade = AddParameter(new ToolParameter(ParamMinAcceptableGrade, "Min Acceptable Grade (Numeric)", ParameterType.Double, 2.5, min: 0.0, max: 4.0));
        _pRoiX = AddParameter(new ToolParameter(ParamRoiX, "ROI X", ParameterType.Double, 0.0, min: 0.0, max: 10000.0, unit: "px"));
        _pRoiY = AddParameter(new ToolParameter(ParamRoiY, "ROI Y", ParameterType.Double, 0.0, min: 0.0, max: 10000.0, unit: "px"));
        _pRoiW = AddParameter(new ToolParameter(ParamRoiWidth, "ROI Width", ParameterType.Double, 0.0, min: 0.0, max: 10000.0, unit: "px"));
        _pRoiH = AddParameter(new ToolParameter(ParamRoiHeight, "ROI Height", ParameterType.Double, 0.0, min: 0.0, max: 10000.0, unit: "px"));

        AddInput(new ToolPort(PortInImage, "Barcode Image Input", PortType.Image, PortDirection.Input));
        AddInput(new ToolPort(PortInDecodedText, "Decoded Text In (Optional)", PortType.String, PortDirection.Input));

        AddOutput(new ToolPort(PortDecodedText, "Decoded Text", PortType.String, PortDirection.Output));
        AddOutput(new ToolPort(PortLetterGrade, "Overall Letter Grade (A-F)", PortType.String, PortDirection.Output));
        AddOutput(new ToolPort(PortNumericGrade, "Numeric Grade (0.0 - 4.0)", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortContrastGrade, "Symbol Contrast Grade", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortAnuGrade, "Axial Non-Uniformity Grade", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortModGrade, "Modulation Grade", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortPassGrade, "Meets Min Grade (Pass/Fail)", PortType.Boolean, PortDirection.Output));
        AddOutput(new ToolPort(PortOutImage, "Annotated Image", PortType.Image, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext context, CancellationToken ct)
    {
        var inputFrame = context.GetImage(PortInImage);
        if (inputFrame == null || inputFrame.Mat == null || inputFrame.Mat.Empty())
            return ToolResult.Failed("Input image is missing or empty.");

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

        string decodedText = context.GetInput<string>(PortInDecodedText) ?? string.Empty;
        Rect? bounds = null;

        // Attempt ZXing decode if text not provided or to find bounds
        try
        {
            using var gray = new Mat();
            if (scanMat.Channels() > 1)
                Cv2.CvtColor(scanMat, gray, ColorConversionCodes.BGR2GRAY);
            else
                scanMat.CopyTo(gray);

            var lumSource = new MatLuminanceSource(gray);
            var reader = new BarcodeReaderGeneric
            {
                AutoRotate = true,
                Options = new ZXing.Common.DecodingOptions { TryHarder = true }
            };

            var zxResult = reader.Decode(lumSource);
            if (zxResult != null)
            {
                if (string.IsNullOrEmpty(decodedText))
                    decodedText = zxResult.Text;

                if (zxResult.ResultPoints != null && zxResult.ResultPoints.Length >= 2)
                {
                    float minX = zxResult.ResultPoints.Min(p => p.X) + scanRect.X;
                    float maxX = zxResult.ResultPoints.Max(p => p.X) + scanRect.X;
                    float minY = zxResult.ResultPoints.Min(p => p.Y) + scanRect.Y;
                    float maxY = zxResult.ResultPoints.Max(p => p.Y) + scanRect.Y;
                    bounds = new Rect((int)minX, (int)minY, (int)Math.Max(1, maxX - minX), (int)Math.Max(1, maxY - minY));
                }
            }
        }
        catch
        {
            // If barcode cannot be decoded, grading continues with Decode Fail
        }

        if (!bounds.HasValue && useRoi)
        {
            bounds = scanRect;
        }

        var grade = BarcodeGradeEvaluator.Evaluate(scanMat, decodedText, bounds);
        double minReq = _pMinGrade.GetValue<double>();
        bool passed = grade.OverallNumericGrade >= minReq && grade.DecodePassed;

        var overlays = new List<IOverlayPrimitive>();
        var statusColor = passed ? new Scalar(0, 255, 100) : new Scalar(0, 50, 255);

        if (useRoi)
        {
            overlays.Add(new RectOverlay(new Rect2f(scanRect.X, scanRect.Y, scanRect.Width, scanRect.Height), new Scalar(255, 200, 0), Thickness: 1));
        }

        if (bounds.HasValue)
        {
            overlays.Add(new RectOverlay(new Rect2f(bounds.Value.X, bounds.Value.Y, bounds.Value.Width, bounds.Value.Height), statusColor, Thickness: 3));

            if (!string.IsNullOrEmpty(grade.DecodedText))
            {
                overlays.Add(new TextOverlay(
                    $"🏷️ [{grade.OverallLetterGrade} ({grade.OverallNumericGrade:F1})] {grade.DecodedText}",
                    new Point2f(bounds.Value.X, Math.Max(20, bounds.Value.Y - 8)),
                    statusColor,
                    FontScale: 0.55,
                    Thickness: 2));
            }
        }

        if (grade.DecodePassed)
        {
            overlays.Add(new TextOverlay(
                $"📊 ISO/IEC Grade: {grade.OverallLetterGrade} ({grade.OverallNumericGrade:F1}) | SC:{grade.SymbolContrastGrade:F0} MOD:{grade.ModulationGrade:F0} ANU:{grade.AxialNonUniformityGrade:F0} DEF:{grade.DefectsGrade:F0} - {(passed ? "PASS" : "FAIL")}",
                new Point2f(10, 25), statusColor, FontScale: 0.55, Thickness: 2));

            overlays.Add(new TextOverlay(
                $"✅ Mã đọc được: \"{grade.DecodedText}\"",
                new Point2f(10, imgH - 15), statusColor, FontScale: 0.55, Thickness: 2));
        }
        else
        {
            overlays.Add(new TextOverlay(
                "⚠️ Không giải mã được mã quang học (Decode Failed - Grade F)",
                new Point2f(10, 25), new Scalar(0, 50, 255), FontScale: 0.55, Thickness: 2));
        }

        var outputs = new Dictionary<string, object?>
        {
            [PortDecodedText] = grade.DecodedText,
            [PortLetterGrade] = grade.OverallLetterGrade.ToString(),
            [PortNumericGrade] = grade.OverallNumericGrade,
            [PortContrastGrade] = grade.SymbolContrastGrade,
            [PortAnuGrade] = grade.AxialNonUniformityGrade,
            [PortModGrade] = grade.ModulationGrade,
            [PortPassGrade] = passed,
            [PortOutImage] = inputFrame.Clone()
        };

        return ToolResult.Ok(outputs, overlays, message: grade.DecodePassed
            ? $"ISO Grade {grade.OverallLetterGrade} ({grade.OverallNumericGrade:F1}) - {(passed ? "PASS" : "FAIL")} ('{grade.DecodedText}')"
            : "Decode Failed - Grade F");
    }
}
