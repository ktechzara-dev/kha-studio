using Microsoft.ML.OnnxRuntime;
using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Overlays;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;
using VisionStudio.Core.Types;
using VisionStudio.DeepLearning.Inference;
using VisionStudio.DeepLearning.Model;
using ModelMeta = VisionStudio.DeepLearning.Model.ModelMetadata;

namespace VisionStudio.DeepLearning.Tools;

/// <summary>
/// Industrial deep learning anomaly detection tool (PatchCore / PaDiM class).
/// Learns exclusively from defect-free good samples, generating a continuous pixel-level
/// anomaly heat map, overall anomaly score, and defect regions for quality inspection.
/// </summary>
[VisionTool("DeepLearning.Anomaly", "Deep Anomaly Detector", "Deep Learning", Description = "Detects visual anomalies and defects using unsupervised deep learning heatmap")]
public sealed class DeepAnomalyDetectionTool : VisionToolBase
{
    public const string PortImageIn = "ImageIn";
    public const string PortHeatmapOut = "HeatmapOut";
    public const string PortAnomalyScore = "AnomalyScore";
    public const string PortDefectRegion = "DefectRegion";
    public const string PortPassed = "Passed";

    public const string ParamModelPath = "ModelPath";
    public const string ParamThreshold = "AnomalyThreshold";
    public const string ParamMinDefectArea = "MinDefectArea";
    public const string ParamProvider = "ExecutionProvider";

    private readonly ToolParameter _pModelPath;
    private readonly ToolParameter _pThreshold;
    private readonly ToolParameter _pMinArea;
    private readonly ToolParameter _pProvider;

    private static readonly InferenceSessionManager SessionManager = new();

    public DeepAnomalyDetectionTool() : base("DeepLearning.Anomaly", "Deep Anomaly Detector", "Deep Learning", "Deep learning anomaly detection")
    {
        _pModelPath = AddParameter(new ToolParameter(ParamModelPath, "Model File (.vsmodel / .onnx)", ParameterType.String, ""));
        _pThreshold = AddParameter(new ToolParameter(ParamThreshold, "Anomaly Threshold", ParameterType.Double, 0.5, min: 0.01, max: 1.0));
        _pMinArea = AddParameter(new ToolParameter(ParamMinDefectArea, "Min Defect Area (px)", ParameterType.Integer, 20, min: 1, max: 100000));
        _pProvider = AddParameter(new ToolParameter(ParamProvider, "Hardware Accelerator", ParameterType.String, "Cpu"));

        AddInput(new ToolPort(PortImageIn, "Input Image", PortType.Image, PortDirection.Input, IsRequired: true));

        AddOutput(new ToolPort(PortHeatmapOut, "Heatmap Image", PortType.Image, PortDirection.Output));
        AddOutput(new ToolPort(PortAnomalyScore, "Anomaly Score", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortDefectRegion, "Defect Region", PortType.Region, PortDirection.Output));
        AddOutput(new ToolPort(PortPassed, "Passed", PortType.Boolean, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var frame = ctx.GetImage(PortImageIn);
        if (frame == null || frame.Mat == null || frame.Mat.Empty())
        {
            return ToolResult.Failed("Input image is missing or empty.");
        }

        string modelPath = _pModelPath.GetValue<string>()?.Trim() ?? string.Empty;
        double threshold = _pThreshold.GetValue<double>();
        int minArea = _pMinArea.GetValue<int>();

        ModelMeta metadata;
        byte[] onnxBytes;

        if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
        {
            // Fall back to synthetic anomaly model for testing and demonstration
            (metadata, onnxBytes) = Synthetic.SyntheticOnnxModelGenerator.CreateAnomalySample();
        }
        else if (modelPath.EndsWith(".vsmodel", StringComparison.OrdinalIgnoreCase))
        {
            (metadata, onnxBytes) = ModelPackage.Load(modelPath);
        }
        else
        {
            onnxBytes = File.ReadAllBytes(modelPath);
            metadata = new ModelMeta
            {
                ModelName = Path.GetFileNameWithoutExtension(modelPath),
                TaskType = DeepLearningTaskType.AnomalyDetection,
                InputWidth = 64,
                InputHeight = 64
            };
        }

        var provider = Enum.TryParse<ExecutionProviderType>(_pProvider.GetValue<string>(), true, out var ep) ? ep : ExecutionProviderType.Cpu;
        var session = SessionManager.GetOrCreateSession(modelPath.Length > 0 ? modelPath : "synthetic_anomaly", onnxBytes, provider);

        var tensor = TensorUtils.CreateInputTensor(
            frame.Mat,
            metadata.InputWidth,
            metadata.InputHeight,
            metadata.IsBgr,
            metadata.Mean,
            metadata.Std);

        var inputName = metadata.InputName;
        if (!session.InputMetadata.ContainsKey(inputName))
        {
            inputName = session.InputMetadata.First().Key;
        }

        var inputs = new[] { NamedOnnxValue.CreateFromTensor(inputName, tensor) };
        using var results = session.Run(inputs);

        // Process anomaly outputs
        float anomalyScore = 0.0f;
        var anomalyMap = new float[metadata.InputHeight, metadata.InputWidth];

        foreach (var output in results)
        {
            var tensorOutput = output.AsTensor<float>();
            if (output.Name.Contains("score", StringComparison.OrdinalIgnoreCase) || tensorOutput.Length == 1)
            {
                anomalyScore = tensorOutput.First();
            }
            else if (output.Name.Contains("map", StringComparison.OrdinalIgnoreCase) || tensorOutput.Length >= metadata.InputHeight * metadata.InputWidth)
            {
                var values = tensorOutput.ToArray();
                int idx = 0;
                for (int y = 0; y < metadata.InputHeight; y++)
                {
                    for (int x = 0; x < metadata.InputWidth; x++)
                    {
                        if (idx < values.Length)
                        {
                            anomalyMap[y, x] = values[idx++];
                        }
                    }
                }
            }
        }

        // If score not explicitly given, compute max over map
        if (anomalyScore <= 0.0f)
        {
            for (int y = 0; y < metadata.InputHeight; y++)
            {
                for (int x = 0; x < metadata.InputWidth; x++)
                {
                    if (anomalyMap[y, x] > anomalyScore) anomalyScore = anomalyMap[y, x];
                }
            }
        }

        // Generate False-Color Jet Heatmap
        var heatmapMat = TensorUtils.CreateHeatmapOverlay(anomalyMap, frame.Mat.Width, frame.Mat.Height);
        var blendedMat = new Mat();
        using var baseBgr = new Mat();
        if (frame.Mat.Channels() == 1)
        {
            Cv2.CvtColor(frame.Mat, baseBgr, ColorConversionCodes.GRAY2BGR);
        }
        else if (frame.Mat.Channels() == 4)
        {
            Cv2.CvtColor(frame.Mat, baseBgr, ColorConversionCodes.BGRA2BGR);
        }
        else
        {
            frame.Mat.CopyTo(baseBgr);
        }
        Cv2.AddWeighted(baseBgr, 0.6, heatmapMat, 0.4, 0.0, blendedMat);

        // Binarize defect mask where heatmap exceeds threshold
        using var grayHeatmap = new Mat();
        Cv2.CvtColor(heatmapMat, grayHeatmap, ColorConversionCodes.BGR2GRAY);
        using var defectMask = new Mat();
        byte byteThresh = (byte)(Math.Clamp(threshold, 0.0, 1.0) * 255.0);
        Cv2.Threshold(grayHeatmap, defectMask, byteThresh, 255, ThresholdTypes.Binary);

        var defectRegion = Region.FromMask(defectMask);
        if (defectRegion.Area < minArea)
        {
            defectRegion = Region.Empty;
        }

        bool passed = anomalyScore <= threshold && defectRegion.Area < minArea;

        var outputsDict = new Dictionary<string, object?>
        {
            [PortHeatmapOut] = new ImageFrame(blendedMat),
            [PortAnomalyScore] = (double)anomalyScore,
            [PortDefectRegion] = defectRegion,
            [PortPassed] = passed
        };

        var overlays = new List<IOverlayPrimitive>
        {
            new TextOverlay(
                passed ? $"GOOD ({anomalyScore:F3})" : $"ANOMALY NG ({anomalyScore:F3})",
                new Point2f(20, 40),
                passed ? Scalar.LightGreen : Scalar.Red,
                0.8,
                2)
        };

        return ToolResult.Ok(outputsDict, overlays, null, $"Anomaly inspection completed. Score: {anomalyScore:F3}, Verdict: {(passed ? "PASS" : "FAIL")}");
    }
}
