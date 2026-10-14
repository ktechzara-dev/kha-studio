using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Filtering;

/// <summary>
/// Frequency-domain 2D Discrete Fourier Transform (DFT) filter for periodic pattern attenuation and defect isolation.
/// </summary>
[VisionTool("Filter.FftBandpass", "FFT Band-Pass Filter", "Filtering", Description = "2D Fourier frequency domain band-pass / notch filtering")]
public sealed class FftBandpassTool : VisionToolBase
{
    public const string ParamMode = "FilterMode";
    public const string ParamLowCutoff = "LowCutoffRadius";
    public const string ParamHighCutoff = "HighCutoffRadius";

    public const string PortInImage = "ImageIn";
    public const string PortOutImage = "ImageOut";
    public const string PortMagnitude = "MagnitudeSpectrum";

    private readonly ToolParameter _pMode;
    private readonly ToolParameter _pLow;
    private readonly ToolParameter _pHigh;

    public FftBandpassTool() : base("Filter.FftBandpass", "FFT Band-Pass Filter", "Filtering", "2D Fourier frequency filtering")
    {
        _pMode = AddParameter(new ToolParameter(ParamMode, "Filter Mode", ParameterType.Enum, "BandPass", enumOptions: new[] { "LowPass", "HighPass", "BandPass" }));
        _pLow = AddParameter(new ToolParameter(ParamLowCutoff, "Low Cutoff Radius", ParameterType.Double, 10.0, min: 0.0, max: 1000.0));
        _pHigh = AddParameter(new ToolParameter(ParamHighCutoff, "High Cutoff Radius", ParameterType.Double, 60.0, min: 1.0, max: 1000.0));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));
        AddOutput(new ToolPort(PortOutImage, "Image Out", PortType.Image, PortDirection.Output));
        AddOutput(new ToolPort(PortMagnitude, "Magnitude Spectrum", PortType.Image, PortDirection.Output));
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

        // Expand to optimal DFT size
        var optRows = Cv2.GetOptimalDFTSize(gray.Rows);
        var optCols = Cv2.GetOptimalDFTSize(gray.Cols);
        using var padded = new Mat();
        Cv2.CopyMakeBorder(gray, padded, 0, optRows - gray.Rows, 0, optCols - gray.Cols, BorderTypes.Constant, Scalar.All(0));

        using var paddedF = new Mat();
        padded.ConvertTo(paddedF, MatType.CV_32F);

        // Complex plane: [Re, Im]
        using var zeroIm = Mat.Zeros(paddedF.Size(), MatType.CV_32F).ToMat();
        using var complexMat = new Mat();
        Cv2.Merge(new[] { paddedF, zeroIm }, complexMat);

        // Forward DFT
        Cv2.Dft(complexMat, complexMat, DftFlags.None);

        // Shift zero frequency to center
        FftShift(complexMat);

        // Generate frequency filter mask
        var mode = _pMode.GetValue<string>() ?? "BandPass";
        var rLow = _pLow.GetValue<double>();
        var rHigh = _pHigh.GetValue<double>();
        using var filterMask = CreateFilterMask(optRows, optCols, mode, rLow, rHigh);

        // Apply filter mask to both Real and Imaginary components
        var planes = Cv2.Split(complexMat);
        Cv2.Multiply(planes[0], filterMask, planes[0]);
        Cv2.Multiply(planes[1], filterMask, planes[1]);
        Cv2.Merge(planes, complexMat);

        // Compute magnitude spectrum for visualization
        using var mag = new Mat();
        Cv2.Magnitude(planes[0], planes[1], mag);
        Cv2.Add(mag, Scalar.All(1.0), mag);
        Cv2.Log(mag, mag);
        Cv2.Normalize(mag, mag, 0, 255, NormTypes.MinMax);
        var mag8U = new Mat();
        mag.ConvertTo(mag8U, MatType.CV_8U);

        foreach (var p in planes) p.Dispose();

        // Shift back
        FftShift(complexMat);

        // Inverse DFT
        using var idft = new Mat();
        Cv2.Idft(complexMat, idft, DftFlags.Scale | DftFlags.RealOutput);

        // Crop back to original dimensions
        using var croppedResult = new Mat(idft, new Rect(0, 0, gray.Cols, gray.Rows));
        Cv2.Normalize(croppedResult, croppedResult, 0, 255, NormTypes.MinMax);

        var dst = new Mat();
        croppedResult.ConvertTo(dst, MatType.CV_8U);

        var outFrame = new ImageFrame(dst, inputFrame.CameraId, inputFrame.FrameId, PixelFormat.Mono8);
        var magFrame = new ImageFrame(mag8U, inputFrame.CameraId, inputFrame.FrameId, PixelFormat.Mono8);

        var outputs = new Dictionary<string, object?>
        {
            [PortOutImage] = outFrame,
            [PortMagnitude] = magFrame
        };

        return ToolResult.Ok(outputs, message: $"FFT {mode} filter completed");
    }

    private static void FftShift(Mat mat)
    {
        var cx = mat.Cols / 2;
        var cy = mat.Rows / 2;

        using var q0 = new Mat(mat, new Rect(0, 0, cx, cy));
        using var q1 = new Mat(mat, new Rect(cx, 0, cx, cy));
        using var q2 = new Mat(mat, new Rect(0, cy, cx, cy));
        using var q3 = new Mat(mat, new Rect(cx, cy, cx, cy));

        using var tmp = new Mat();
        q0.CopyTo(tmp);
        q3.CopyTo(q0);
        tmp.CopyTo(q3);

        q1.CopyTo(tmp);
        q2.CopyTo(q1);
        tmp.CopyTo(q2);
    }

    private static Mat CreateFilterMask(int rows, int cols, string mode, double rLow, double rHigh)
    {
        var mask = new Mat(rows, cols, MatType.CV_32F, Scalar.All(0));
        var cx = cols / 2.0;
        var cy = rows / 2.0;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                var d = Math.Sqrt((c - cx) * (c - cx) + (r - cy) * (r - cy));
                float weight = 0f;

                switch (mode.ToLowerInvariant())
                {
                    case "lowpass":
                        weight = d <= rHigh ? 1f : 0f;
                        break;
                    case "highpass":
                        weight = d >= rLow ? 1f : 0f;
                        break;
                    default: // bandpass
                        weight = (d >= rLow && d <= rHigh) ? 1f : 0f;
                        break;
                }

                mask.Set(r, c, weight);
            }
        }

        return mask;
    }
}
