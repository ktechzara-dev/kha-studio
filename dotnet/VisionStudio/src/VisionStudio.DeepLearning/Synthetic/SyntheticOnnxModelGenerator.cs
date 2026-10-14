using System.Text;
using VisionStudio.DeepLearning.Model;

namespace VisionStudio.DeepLearning.Synthetic;

/// <summary>
/// Lightweight in-memory ONNX binary protobuf generator for creating valid synthetic test models
/// without external PyTorch/pip dependencies. Used in unit tests and automated CI pipelines.
/// </summary>
public static class SyntheticOnnxModelGenerator
{
    /// <summary>
    /// Creates a valid ONNX model containing a single Identity operation (or multi-node graph)
    /// with the specified input and output tensor dimensions.
    /// </summary>
    public static byte[] CreateModel(
        string modelName,
        string opType,
        string inputName,
        int[] inputShape,
        string outputName,
        int[] outputShape,
        int elemType = 1) // 1 = TensorProto.FLOAT
    {
        // 1. Build NodeProto
        var nodeWriter = new ProtoWriter();
        nodeWriter.WriteString(1, inputName);    // repeated input
        nodeWriter.WriteString(2, outputName);   // repeated output
        nodeWriter.WriteString(3, "node_" + opType); // name
        nodeWriter.WriteString(4, opType);       // op_type
        nodeWriter.WriteString(7, "");           // domain
        var nodeBytes = nodeWriter.ToArray();

        // 2. Build Input ValueInfoProto
        var inputValBytes = BuildValueInfo(inputName, inputShape, elemType);

        // 3. Build Output ValueInfoProto
        var outputValBytes = BuildValueInfo(outputName, outputShape, elemType);

        // 4. Build GraphProto
        var graphWriter = new ProtoWriter();
        graphWriter.WriteMessage(1, nodeBytes);       // repeated node
        graphWriter.WriteString(2, modelName);        // name
        graphWriter.WriteMessage(11, inputValBytes);  // repeated input
        graphWriter.WriteMessage(12, outputValBytes); // repeated output
        var graphBytes = graphWriter.ToArray();

        // 5. Build OperatorSetIdProto (opset 14)
        var opsetWriter = new ProtoWriter();
        opsetWriter.WriteString(1, ""); // domain
        opsetWriter.WriteVarint(2, 14); // version
        var opsetBytes = opsetWriter.ToArray();

        // 6. Build ModelProto
        var modelWriter = new ProtoWriter();
        modelWriter.WriteVarint(1, 7);                  // ir_version = 7
        modelWriter.WriteString(2, "VisionStudio");     // producer_name
        modelWriter.WriteMessage(7, graphBytes);        // graph
        modelWriter.WriteMessage(8, opsetBytes);        // repeated opset_import
        return modelWriter.ToArray();
    }

    /// <summary>
    /// Builds a multi-output synthetic ONNX model (e.g. for Anomaly Detection: score and heatmap).
    /// </summary>
    public static byte[] CreateMultiOutputModel(
        string modelName,
        string inputName,
        int[] inputShape,
        (string opType, string outName, int[] outShape)[] outputs,
        int elemType = 1)
    {
        var graphWriter = new ProtoWriter();

        // Add nodes
        foreach (var (opType, outName, _) in outputs)
        {
            var nodeWriter = new ProtoWriter();
            nodeWriter.WriteString(1, inputName);
            nodeWriter.WriteString(2, outName);
            nodeWriter.WriteString(3, "node_" + outName);
            nodeWriter.WriteString(4, opType);
            graphWriter.WriteMessage(1, nodeWriter.ToArray());
        }

        // Graph name
        graphWriter.WriteString(2, modelName);

        // Input
        graphWriter.WriteMessage(11, BuildValueInfo(inputName, inputShape, elemType));

        // Outputs
        foreach (var (_, outName, outShape) in outputs)
        {
            graphWriter.WriteMessage(12, BuildValueInfo(outName, outShape, elemType));
        }

        var graphBytes = graphWriter.ToArray();

        var opsetWriter = new ProtoWriter();
        opsetWriter.WriteString(1, "");
        opsetWriter.WriteVarint(2, 14);

        var modelWriter = new ProtoWriter();
        modelWriter.WriteVarint(1, 7);
        modelWriter.WriteString(2, "VisionStudio");
        modelWriter.WriteMessage(7, graphBytes);
        modelWriter.WriteMessage(8, opsetWriter.ToArray());
        return modelWriter.ToArray();
    }

    private static byte[] BuildValueInfo(string name, int[] shape, int elemType)
    {
        // Dimension protos
        var shapeWriter = new ProtoWriter();
        foreach (int dim in shape)
        {
            var dimWriter = new ProtoWriter();
            dimWriter.WriteVarint(1, dim); // dim_value
            shapeWriter.WriteMessage(1, dimWriter.ToArray()); // repeated dim
        }

        // TensorTypeProto
        var tensorTypeWriter = new ProtoWriter();
        tensorTypeWriter.WriteVarint(1, elemType); // elem_type = FLOAT
        tensorTypeWriter.WriteMessage(2, shapeWriter.ToArray()); // shape

        // TypeProto
        var typeWriter = new ProtoWriter();
        typeWriter.WriteMessage(1, tensorTypeWriter.ToArray()); // tensor_type

        // ValueInfoProto
        var valWriter = new ProtoWriter();
        valWriter.WriteString(1, name);
        valWriter.WriteMessage(2, typeWriter.ToArray());
        return valWriter.ToArray();
    }

    public static (ModelMetadata Metadata, byte[] OnnxBytes) CreateClassificationSample(int numClasses = 3)
    {
        var meta = new ModelMetadata
        {
            ModelName = "SyntheticClassifier",
            TaskType = DeepLearningTaskType.Classification,
            InputName = "input",
            InputWidth = 64,
            InputHeight = 64,
            OutputNames = new List<string> { "output" },
            ClassLabels = Enumerable.Range(0, numClasses).Select(i => $"Class_{i}").ToList()
        };

        // Input [1, 3, 64, 64] -> Identity -> Output [1, 3, 64, 64]
        var onnx = CreateModel(
            meta.ModelName,
            "Relu",
            meta.InputName,
            new[] { 1, 3, 64, 64 },
            "output",
            new[] { 1, 3, 64, 64 });

        return (meta, onnx);
    }

    public static (ModelMetadata Metadata, byte[] OnnxBytes) CreateAnomalySample()
    {
        var meta = new ModelMetadata
        {
            ModelName = "SyntheticAnomaly",
            TaskType = DeepLearningTaskType.AnomalyDetection,
            InputName = "input",
            InputWidth = 64,
            InputHeight = 64,
            OutputNames = new List<string> { "anomaly_score", "anomaly_map" },
            DefaultAnomalyThreshold = 0.5f
        };

        var onnx = CreateMultiOutputModel(
            meta.ModelName,
            meta.InputName,
            new[] { 1, 3, 64, 64 },
            new[]
            {
                ("Identity", "anomaly_score", new[] { 1, 3, 64, 64 }),
                ("Relu", "anomaly_map", new[] { 1, 3, 64, 64 })
            });

        return (meta, onnx);
    }

    public static (ModelMetadata Metadata, byte[] OnnxBytes) CreateObjectDetectionSample()
    {
        var meta = new ModelMetadata
        {
            ModelName = "SyntheticDetector",
            TaskType = DeepLearningTaskType.ObjectDetection,
            InputName = "input",
            InputWidth = 128,
            InputHeight = 128,
            OutputNames = new List<string> { "boxes" },
            ClassLabels = new List<string> { "Cap", "Pin", "Scratch" }
        };

        var onnx = CreateModel(
            meta.ModelName,
            "Relu",
            meta.InputName,
            new[] { 1, 3, 128, 128 },
            "boxes",
            new[] { 1, 3, 128, 128 });

        return (meta, onnx);
    }

    public static (ModelMetadata Metadata, byte[] OnnxBytes) CreateSegmentationSample()
    {
        var meta = new ModelMetadata
        {
            ModelName = "SyntheticSegmenter",
            TaskType = DeepLearningTaskType.InstanceSegmentation,
            InputName = "input",
            InputWidth = 64,
            InputHeight = 64,
            OutputNames = new List<string> { "masks" },
            ClassLabels = new List<string> { "Background", "Weld", "Pore" }
        };

        var onnx = CreateModel(
            meta.ModelName,
            "Relu",
            meta.InputName,
            new[] { 1, 3, 64, 64 },
            "masks",
            new[] { 1, 3, 64, 64 });

        return (meta, onnx);
    }

    public static (ModelMetadata Metadata, byte[] OnnxBytes) CreateOcrSample()
    {
        var meta = new ModelMetadata
        {
            ModelName = "SyntheticDeepOcr",
            TaskType = DeepLearningTaskType.DeepOcr,
            InputName = "input",
            InputWidth = 100,
            InputHeight = 32,
            OutputNames = new List<string> { "text_logits" },
            ClassLabels = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ-".Select(c => c.ToString()).ToList()
        };

        var onnx = CreateModel(
            meta.ModelName,
            "Relu",
            meta.InputName,
            new[] { 1, 3, 32, 100 },
            "text_logits",
            new[] { 1, 3, 32, 100 });

        return (meta, onnx);
    }

    private sealed class ProtoWriter
    {
        private readonly MemoryStream _ms = new();

        public void WriteVarint(int fieldNumber, long value)
        {
            WriteTag(fieldNumber, 0);
            WriteRawVarint((ulong)value);
        }

        public void WriteString(int fieldNumber, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            WriteBytes(fieldNumber, bytes);
        }

        public void WriteBytes(int fieldNumber, byte[] bytes)
        {
            WriteTag(fieldNumber, 2);
            WriteRawVarint((ulong)bytes.Length);
            _ms.Write(bytes);
        }

        public void WriteMessage(int fieldNumber, byte[] messageBytes)
        {
            WriteBytes(fieldNumber, messageBytes);
        }

        private void WriteTag(int fieldNumber, int wireType)
        {
            WriteRawVarint((ulong)((fieldNumber << 3) | wireType));
        }

        private void WriteRawVarint(ulong value)
        {
            while (value >= 0x80)
            {
                _ms.WriteByte((byte)((value & 0x7F) | 0x80));
                value >>= 7;
            }
            _ms.WriteByte((byte)(value & 0x7F));
        }

        public byte[] ToArray() => _ms.ToArray();
    }
}
