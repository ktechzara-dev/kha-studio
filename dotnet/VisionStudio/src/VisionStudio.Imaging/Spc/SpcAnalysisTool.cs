using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Overlays;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Spc;

/// <summary>
/// Statistical Process Control (SPC) Analysis Tool tracking rolling measurement samples, computing Cp, Cpk, and 3-sigma control limits.
/// </summary>
[VisionTool("Spc.ProcessCapability", "SPC & Process Capability (Cp/Cpk)", "Measurement", Description = "Tracks live measurements, evaluates Cp/Cpk, and detects out-of-control conditions")]
public sealed class SpcAnalysisTool : VisionToolBase
{
    public const string PortInValue = "ValueIn";
    public const string PortMean = "Mean";
    public const string PortStdDev = "StdDev";
    public const string PortCp = "Cp";
    public const string PortCpk = "Cpk";
    public const string PortUcl = "UCL";
    public const string PortLcl = "LCL";
    public const string PortInControl = "InControl";

    public const string ParamLsl = "LowerSpecLimit";
    public const string ParamUsl = "UpperSpecLimit";
    public const string ParamMaxSamples = "MaxSamples";

    private readonly List<double> _samples = new();
    private readonly object _lock = new();

    private readonly ToolParameter _pLsl;
    private readonly ToolParameter _pUsl;
    private readonly ToolParameter _pMaxSamples;

    public SpcAnalysisTool() : base("Spc.ProcessCapability", "SPC & Process Capability (Cp/Cpk)", "Measurement", "Tracks live measurements, evaluates Cp/Cpk, and detects out-of-control conditions")
    {
        _pLsl = AddParameter(new ToolParameter(ParamLsl, "Lower Spec Limit (LSL)", ParameterType.Double, 24.5, min: -100000.0, max: 100000.0));
        _pUsl = AddParameter(new ToolParameter(ParamUsl, "Upper Spec Limit (USL)", ParameterType.Double, 25.5, min: -100000.0, max: 100000.0));
        _pMaxSamples = AddParameter(new ToolParameter(ParamMaxSamples, "Max Rolling Samples", ParameterType.Integer, 50, min: 10, max: 1000));

        AddInput(new ToolPort(PortInValue, "Measurement Value", PortType.Number, PortDirection.Input));
        AddOutput(new ToolPort(PortMean, "Process Mean", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortStdDev, "Sigma StdDev", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortCp, "Process Potential (Cp)", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortCpk, "Process Capability (Cpk)", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortUcl, "Upper Control Limit (UCL)", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortLcl, "Lower Control Limit (LCL)", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortInControl, "In Control (Pass/Fail)", PortType.Boolean, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext context, CancellationToken ct)
    {
        double val = context.GetInput<double>(PortInValue);
        double lsl = _pLsl.GetValue<double>();
        double usl = _pUsl.GetValue<double>();
        int maxSamples = _pMaxSamples.GetValue<int>();

        List<double> currentList;
        lock (_lock)
        {
            _samples.Add(val);
            if (_samples.Count > maxSamples)
                _samples.RemoveAt(0);

            currentList = new List<double>(_samples);
        }

        var spc = MsaEngine.EvaluateCapability(currentList, lsl, usl);

        var overlays = new List<IOverlayPrimitive>
        {
            new TextOverlay($"SPC: N={spc.SampleCount} | Mean: {spc.Mean:F3} | Cpk: {spc.Cpk:F2} [{(spc.InControl ? "IN CONTROL" : "WARNING")}]",
                new Point2f(10, 25), spc.InControl ? new Scalar(0, 255, 100) : new Scalar(0, 170, 255), FontScale: 0.5)
        };

        var outputs = new Dictionary<string, object?>
        {
            [PortMean] = spc.Mean,
            [PortStdDev] = spc.StdDev,
            [PortCp] = spc.Cp,
            [PortCpk] = spc.Cpk,
            [PortUcl] = spc.UpperControlLimit,
            [PortLcl] = spc.LowerControlLimit,
            [PortInControl] = spc.InControl
        };

        return ToolResult.Ok(outputs, overlays, message: $"Cpk={spc.Cpk:F2} ({(spc.InControl ? "In Control" : "Out of Control")})");
    }
}
