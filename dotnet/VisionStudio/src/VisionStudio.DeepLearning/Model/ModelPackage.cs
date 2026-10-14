using System.IO.Compression;
using System.Text;

namespace VisionStudio.DeepLearning.Model;

/// <summary>
/// Packages and unpacks VisionStudio Deep Learning models (.vsmodel).
/// A .vsmodel archive bundles the ONNX binary model, preprocessing specifications, and labels.
/// </summary>
public static class ModelPackage
{
    public const string MetadataEntryName = "metadata.json";
    public const string OnnxEntryName = "model.onnx";

    public static void Save(string filePath, ModelMetadata metadata, byte[] onnxBytes)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var fs = File.Create(filePath);
        SaveToStream(fs, metadata, onnxBytes);
    }

    public static void SaveToStream(Stream stream, ModelMetadata metadata, byte[] onnxBytes)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);

        // Write metadata.json
        var metaEntry = archive.CreateEntry(MetadataEntryName, CompressionLevel.Optimal);
        using (var metaStream = metaEntry.Open())
        using (var writer = new StreamWriter(metaStream, Encoding.UTF8))
        {
            writer.Write(metadata.ToJson());
        }

        // Write model.onnx
        var onnxEntry = archive.CreateEntry(OnnxEntryName, CompressionLevel.NoCompression);
        using (var onnxStream = onnxEntry.Open())
        {
            onnxStream.Write(onnxBytes, 0, onnxBytes.Length);
        }
    }

    public static (ModelMetadata Metadata, byte[] OnnxBytes) Load(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Model package file not found: {filePath}", filePath);
        }

        using var fs = File.OpenRead(filePath);
        return LoadFromStream(fs);
    }

    public static (ModelMetadata Metadata, byte[] OnnxBytes) LoadFromStream(Stream stream)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);

        var metaEntry = archive.GetEntry(MetadataEntryName)
            ?? throw new InvalidDataException($"Missing '{MetadataEntryName}' in model package.");

        var onnxEntry = archive.GetEntry(OnnxEntryName)
            ?? throw new InvalidDataException($"Missing '{OnnxEntryName}' in model package.");

        ModelMetadata metadata;
        using (var metaStream = metaEntry.Open())
        using (var reader = new StreamReader(metaStream, Encoding.UTF8))
        {
            string json = reader.ReadToEnd();
            metadata = ModelMetadata.FromJson(json);
        }

        byte[] onnxBytes;
        using (var onnxStream = onnxEntry.Open())
        using (var ms = new MemoryStream())
        {
            onnxStream.CopyTo(ms);
            onnxBytes = ms.ToArray();
        }

        return (metadata, onnxBytes);
    }
}
