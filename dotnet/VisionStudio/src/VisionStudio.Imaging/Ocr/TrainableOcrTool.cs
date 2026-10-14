using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Overlays;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Ocr;

/// <summary>
/// Industrial Trainable OCR / OCV Tool.
/// Performs character segmentation, template correlation, and verification against expected string.
/// </summary>
[VisionTool("Ocr.TrainableOcr", "Trainable OCR / OCV", "Identification", Description = "Optical Character Recognition & Verification with per-character confidence")]
public sealed class TrainableOcrTool : VisionToolBase
{
    public const string PortInImage = "ImageIn";
    public const string PortReadText = "ReadText";
    public const string PortScore = "OverallScore";
    public const string PortVerified = "IsVerified";
    public const string PortCharCount = "CharCount";
    public const string PortOutImage = "AnnotatedImage";

    public const string ParamExpected = "ExpectedString";
    public const string ParamMinConf = "MinConfidence";

    private readonly TrainableOcrEngine _engine;
    private readonly ToolParameter _pExpected;
    private readonly ToolParameter _pMinConf;

    public TrainableOcrTool() : base("Ocr.TrainableOcr", "Trainable OCR / OCV", "Identification", "Optical Character Recognition & Verification with per-character confidence")
    {
        _engine = new TrainableOcrEngine();

        _pExpected = AddParameter(new ToolParameter(ParamExpected, "Expected String (OCV)", ParameterType.String, ""));
        _pMinConf = AddParameter(new ToolParameter(ParamMinConf, "Min Character Confidence", ParameterType.Double, 0.6, min: 0.1, max: 1.0));

        AddInput(new ToolPort(PortInImage, "Input Image", PortType.Image, PortDirection.Input));
        AddOutput(new ToolPort(PortReadText, "Decoded Text", PortType.String, PortDirection.Output));
        AddOutput(new ToolPort(PortScore, "Overall Confidence (0-1)", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortVerified, "Verification Result (Pass/Fail)", PortType.Boolean, PortDirection.Output));
        AddOutput(new ToolPort(PortCharCount, "Character Count", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortOutImage, "Annotated Image", PortType.Image, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext context, CancellationToken ct)
    {
        var inputFrame = context.GetImage(PortInImage);
        if (inputFrame == null || inputFrame.Mat.Empty())
            return ToolResult.Failed("Input image is missing or empty.");

        var expected = _pExpected.GetValue<string>() ?? "";
        var minConf = (float)_pMinConf.GetValue<double>();

        var result = _engine.RecognizeAndVerify(inputFrame.Mat, expected, minConf);

        var overlays = new List<IOverlayPrimitive>();
        foreach (var ch in result.Characters)
        {
            var color = ch.Confidence >= minConf ? new Scalar(0, 255, 100) : new Scalar(0, 50, 255);
            overlays.Add(new RectOverlay(new Rect2f(ch.BoundingBox.X, ch.BoundingBox.Y, ch.BoundingBox.Width, ch.BoundingBox.Height), color, Thickness: 1));
            overlays.Add(new TextOverlay($"{ch.Character} ({ch.Confidence * 100:F0}%)", new Point2f(ch.BoundingBox.X, Math.Max(12, ch.BoundingBox.Y - 4)), color, FontScale: 0.4));
        }

        var headerColor = result.IsVerified ? new Scalar(0, 255, 100) : new Scalar(0, 50, 255);
        overlays.Add(new TextOverlay($"OCR: \"{result.ReadText}\" [{(result.IsVerified ? "VERIFIED" : "MISMATCH")}]",
            new Point2f(10, 25), headerColor, FontScale: 0.6));

        var outputs = new Dictionary<string, object?>
        {
            [PortReadText] = result.ReadText,
            [PortScore] = (double)result.OverallConfidence,
            [PortVerified] = result.IsVerified,
            [PortCharCount] = (double)result.Characters.Count,
            [PortOutImage] = inputFrame.Clone()
        };

        return ToolResult.Ok(outputs, overlays, message: $"OCR Read: '{result.ReadText}' (Conf={result.OverallConfidence:P0})");
    }
}
