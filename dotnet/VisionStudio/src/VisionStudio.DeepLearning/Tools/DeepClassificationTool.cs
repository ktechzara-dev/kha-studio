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
/// Industrial deep learning classification tool.
/// Predicts product part types, surface quality categories, and detects reject classes.
/// </summary>
[VisionTool("DeepLearning.Classification", "Deep Classifier", "Deep Learning", Description = "Classifies parts or defects into labeled categories using deep learning")]
public sealed class DeepClassificationTool : VisionToolBase
{
    public const string PortImageIn = "ImageIn";
    public const string PortTopClass = "TopClass";
    public const string PortConfidence = "Confidence";
    public const string PortPredictionsTable = "PredictionsTable";
    public const string PortPassed = "Passed";

    public const string ParamModelPath = "ModelPath";
    public const string ParamMinConfidence = "MinConfidence";
    public const string ParamTopK = "TopK";
    public const string ParamRejectClass = "RejectClass";
    public const string ParamProvider = "ExecutionProvider";

    private readonly ToolParameter _pModelPath;
    private readonly ToolParameter _pMinConfidence;
    private readonly ToolParameter _pTopK;
    private readonly ToolParameter _pRejectClass;
    private readonly ToolParameter _pProvider;

    private static readonly InferenceSessionManager SessionManager = new();

    public DeepClassificationTool() : base("DeepLearning.Classification", "Deep Classifier", "Deep Learning", "Deep learning classification tool")
    {
        _pModelPath = AddParameter(new ToolParameter(ParamModelPath, "Model File (.vsmodel / .onnx)", ParameterType.String, ""));
        _pMinConfidence = AddParameter(new ToolParameter(ParamMinConfidence, "Min Confidence", ParameterType.Double, 0.7, min: 0.01, max: 1.0));
        _pTopK = AddParameter(new ToolParameter(ParamTopK, "Top K Predictions", ParameterType.Integer, 3, min: 1, max: 20));
        _pRejectClass = AddParameter(new ToolParameter(ParamRejectClass, "Reject Class Name", ParameterType.String, "NG"));
        _pProvider = AddParameter(new ToolParameter(ParamProvider, "Hardware Accelerator", ParameterType.String, "Cpu"));

        AddInput(new ToolPort(PortImageIn, "Input Image", PortType.Image, PortDirection.Input, IsRequired: true));

        AddOutput(new ToolPort(PortTopClass, "Top Class", PortType.String, PortDirection.Output));
        AddOutput(new ToolPort(PortConfidence, "Confidence", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortPredictionsTable, "Predictions Table", PortType.Table, PortDirection.Output));
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
        double minConf = _pMinConfidence.GetValue<double>();
        int topK = _pTopK.GetValue<int>();
        string rejectClass = _pRejectClass.GetValue<string>()?.Trim() ?? string.Empty;

        ModelMeta metadata;
        byte[] onnxBytes;

        if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
        {
            (metadata, onnxBytes) = Synthetic.SyntheticOnnxModelGenerator.CreateClassificationSample(numClasses: 3);
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
                TaskType = DeepLearningTaskType.Classification,
                InputWidth = 64,
                InputHeight = 64,
                ClassLabels = new List<string> { "OK", "NG", "Defect" }
            };
        }

        var provider = Enum.TryParse<ExecutionProviderType>(_pProvider.GetValue<string>(), true, out var ep) ? ep : ExecutionProviderType.Cpu;
        var session = SessionManager.GetOrCreateSession(modelPath.Length > 0 ? modelPath : "synthetic_classifier", onnxBytes, provider);

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

        var firstOutput = results.First().AsTensor<float>();
        var logits = firstOutput.ToArray();

        int numClasses = Math.Max(1, metadata.ClassLabels.Count > 0 ? metadata.ClassLabels.Count : logits.Length);
        var probs = TensorUtils.Softmax(logits.AsSpan(0, Math.Min(numClasses, logits.Length)));

        var ranked = new List<(string Label, double Confidence)>();
        for (int i = 0; i < probs.Length; i++)
        {
            string label = (i < metadata.ClassLabels.Count) ? metadata.ClassLabels[i] : $"Class_{i}";
            ranked.Add((label, Math.Round((double)probs[i], 4)));
        }

        ranked = ranked.OrderByDescending(r => r.Confidence).ToList();
        var top = ranked.First();

        bool passed = top.Confidence >= minConf &&
                      (!string.IsNullOrWhiteSpace(rejectClass) ? !string.Equals(top.Label, rejectClass, StringComparison.OrdinalIgnoreCase) : true);

        // Build predictions VisionTable
        var predTable = new VisionTable();
        predTable.AddColumn("Rank", TableColumnType.String);
        predTable.AddColumn("Class", TableColumnType.String);
        predTable.AddColumn("Confidence", TableColumnType.Number);

        int rankLimit = Math.Min(topK, ranked.Count);
        for (int i = 0; i < rankLimit; i++)
        {
            predTable.AddRow($"#{i + 1}", ranked[i].Label, ranked[i].Confidence);
        }

        var outputsDict = new Dictionary<string, object?>
        {
            [PortTopClass] = top.Label,
            [PortConfidence] = top.Confidence,
            [PortPredictionsTable] = predTable,
            [PortPassed] = passed
        };

        var overlays = new List<IOverlayPrimitive>
        {
            new TextOverlay(
                $"{top.Label} ({top.Confidence * 100:F1}%)",
                new Point2f(20, 40),
                passed ? Scalar.LightGreen : Scalar.Red,
                0.8,
                2)
        };

        return ToolResult.Ok(outputsDict, overlays, null, $"Classified as '{top.Label}' (Conf: {top.Confidence:P1}) - Verdict: {(passed ? "PASS" : "FAIL")}");
    }
}
