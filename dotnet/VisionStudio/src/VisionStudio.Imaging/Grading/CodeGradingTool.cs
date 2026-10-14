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
    public const string PortInImage = "ImageIn";
    public const string PortDecodedText = "DecodedText";
    public const string PortLetterGrade = "OverallGrade";
    public const string PortNumericGrade = "NumericGrade";
    public const string PortContrastGrade = "SymbolContrastGrade";
    public const string PortAnuGrade = "AxialNonUniformityGrade";
    public const string PortModGrade = "ModulationGrade";
    public const string PortPassGrade = "PassGrade";
    public const string PortOutImage = "AnnotatedImage";

    public const string ParamMinAcceptableGrade = "MinAcceptableGrade";

    private readonly ToolParameter _pMinGrade;

    public CodeGradingTool() : base("Grading.CodeQuality", "Code Quality Grading (ISO/IEC)", "Identification", "Evaluates print quality grades (A-F) for 1D and 2D barcodes per ISO/IEC 15415/15416")
    {
        _pMinGrade = AddParameter(new ToolParameter(ParamMinAcceptableGrade, "Min Acceptable Grade (Numeric)", ParameterType.Double, 2.5, min: 0.0, max: 4.0));

        AddInput(new ToolPort(PortInImage, "Barcode Image Input", PortType.Image, PortDirection.Input));
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
        if (inputFrame == null || inputFrame.Mat.Empty())
            return ToolResult.Failed("Input image is missing or empty.");

        string decodedText = "";
        Rect? bounds = null;

        // Attempt ZXing decode
        try
        {
            using var gray = new Mat();
            if (inputFrame.Channels > 1)
                Cv2.CvtColor(inputFrame.Mat, gray, ColorConversionCodes.BGR2GRAY);
            else
                inputFrame.Mat.CopyTo(gray);

            var lumSource = new MatLuminanceSource(gray);
            var reader = new BarcodeReaderGeneric
            {
                AutoRotate = true,
                Options = new ZXing.Common.DecodingOptions { TryHarder = true }
            };

            var zxResult = reader.Decode(lumSource);
            if (zxResult != null)
            {
                decodedText = zxResult.Text;
                if (zxResult.ResultPoints != null && zxResult.ResultPoints.Length >= 2)
                {
                    float minX = zxResult.ResultPoints.Min(p => p.X);
                    float maxX = zxResult.ResultPoints.Max(p => p.X);
                    float minY = zxResult.ResultPoints.Min(p => p.Y);
                    float maxY = zxResult.ResultPoints.Max(p => p.Y);
                    bounds = new Rect((int)minX, (int)minY, (int)Math.Max(1, maxX - minX), (int)Math.Max(1, maxY - minY));
                }
            }
        }
        catch
        {
            // If barcode cannot be decoded, grading continues with Decode Fail
        }

        var grade = BarcodeGradeEvaluator.Evaluate(inputFrame.Mat, decodedText, bounds);
        double minReq = _pMinGrade.GetValue<double>();
        bool passed = grade.OverallNumericGrade >= minReq;

        var overlays = new List<IOverlayPrimitive>();
        if (bounds.HasValue)
        {
            overlays.Add(new RectOverlay(new Rect2f(bounds.Value.X, bounds.Value.Y, bounds.Value.Width, bounds.Value.Height), passed ? new Scalar(0, 255, 100) : new Scalar(0, 50, 255), Thickness: 2));
        }

        overlays.Add(new TextOverlay($"ISO Grade: {grade.OverallLetterGrade} ({grade.OverallNumericGrade:F1}) [SC:{grade.SymbolContrastGrade:F0} ANU:{grade.AxialNonUniformityGrade:F0} MOD:{grade.ModulationGrade:F0}]",
            new Point2f(10, 25), passed ? new Scalar(0, 255, 100) : new Scalar(0, 50, 255), FontScale: 0.5));

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

        return ToolResult.Ok(outputs, overlays, message: $"Grade {grade.OverallLetterGrade} ({grade.OverallNumericGrade:F1}) - {(passed ? "PASS" : "FAIL")}");
    }
}
