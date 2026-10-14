using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Identification;

/// <summary>
/// Measures chromatic color accuracy against a golden color reference in CIE Lab space using Delta E (ΔE).
/// </summary>
[VisionTool("Identification.ColorCheck", "Color Check (Delta E)", "Identification", Description = "Evaluates chromatic distance against reference color in CIE Lab space")]
public sealed class ColorCheckTool : VisionToolBase
{
    public const string ParamRefR = "ReferenceRed";
    public const string ParamRefG = "ReferenceGreen";
    public const string ParamRefB = "ReferenceBlue";
    public const string ParamMaxDeltaE = "MaxAllowedDeltaE";

    public const string PortDeltaE = "DeltaE";
    public const string PortMeanR = "MeanR";
    public const string PortMeanG = "MeanG";
    public const string PortMeanB = "MeanB";
    public const string PortPass = "IsColorMatch";

    private readonly ToolParameter _pR;
    private readonly ToolParameter _pG;
    private readonly ToolParameter _pB;
    private readonly ToolParameter _pMaxDe;

    public ColorCheckTool() : base("Identification.ColorCheck", "Color Check (Delta E)", "Identification", "Color consistency verification")
    {
        _pR = AddParameter(new ToolParameter(ParamRefR, "Ref Red (0-255)", ParameterType.Integer, 200, min: 0, max: 255));
        _pG = AddParameter(new ToolParameter(ParamRefG, "Ref Green (0-255)", ParameterType.Integer, 50, min: 0, max: 255));
        _pB = AddParameter(new ToolParameter(ParamRefB, "Ref Blue (0-255)", ParameterType.Integer, 50, min: 0, max: 255));
        _pMaxDe = AddParameter(new ToolParameter(ParamMaxDeltaE, "Max Tolerance ΔE", ParameterType.Double, 15.0, min: 0.1, max: 100.0));

        AddInput(new ToolPort("ImageIn", "Image In", PortType.Image, PortDirection.Input));

        AddOutput(new ToolPort(PortDeltaE, "Delta E", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortMeanR, "Mean R", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortMeanG, "Mean G", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortMeanB, "Mean B", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortPass, "Is Color Match", PortType.Boolean, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var inputFrame = ctx.GetImage("ImageIn");
        if (inputFrame == null) return ToolResult.Failed("Missing input image.");

        var src = inputFrame.Mat;
        if (src.Channels() < 3) return ToolResult.Failed("Color check requires at least 3 color channels.");

        var meanScalar = Cv2.Mean(src); // OpenCv stores B, G, R
        var meanB = meanScalar.Val0;
        var meanG = meanScalar.Val1;
        var meanR = meanScalar.Val2;

        var refR = _pR.GetValue<int>();
        var refG = _pG.GetValue<int>();
        var refB = _pB.GetValue<int>();

        // Convert both to CIE Lab for perceptually uniform Delta E
        using var curMat = new Mat(1, 1, MatType.CV_8UC3, new Scalar(meanB, meanG, meanR));
        using var refMat = new Mat(1, 1, MatType.CV_8UC3, new Scalar(refB, refG, refR));

        using var curLab = new Mat();
        using var refLab = new Mat();

        Cv2.CvtColor(curMat, curLab, ColorConversionCodes.BGR2Lab);
        Cv2.CvtColor(refMat, refLab, ColorConversionCodes.BGR2Lab);

        var curVec = curLab.Get<Vec3b>(0, 0);
        var refVec = refLab.Get<Vec3b>(0, 0);

        var dL = (double)curVec.Item0 - refVec.Item0;
        var da = (double)curVec.Item1 - refVec.Item1;
        var db = (double)curVec.Item2 - refVec.Item2;

        var deltaE = Math.Sqrt(dL * dL + da * da + db * db);
        var maxAllowed = _pMaxDe.GetValue<double>();
        var isMatch = deltaE <= maxAllowed;

        var outputs = new Dictionary<string, object?>
        {
            [PortDeltaE] = deltaE,
            [PortMeanR] = meanR,
            [PortMeanG] = meanG,
            [PortMeanB] = meanB,
            [PortPass] = isMatch
        };

        return ToolResult.Ok(outputs, message: $"Color ΔE={deltaE:F2} (Max={maxAllowed:F1}, {(isMatch ? "MATCH" : "MISMATCH")})");
    }
}
