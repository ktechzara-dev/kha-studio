using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Acquisition;

/// <summary>
/// Loads a static single image file from disk.
/// </summary>
[VisionTool("Acquisition.ImageFile", "Image File", "Acquisition", Description = "Loads an image from a local file path")]
public sealed class ImageFileTool : VisionToolBase
{
    public const string ParamFilePath = "FilePath";
    public const string PortImage = "Image";

    private readonly ToolParameter _pFilePath;

    public ImageFileTool() : base("Acquisition.ImageFile", "Image File", "Acquisition", "Loads an image from file")
    {
        _pFilePath = AddParameter(new ToolParameter(
            ParamFilePath,
            "File Path",
            ParameterType.FilePath,
            string.Empty,
            tooltip: "Absolute or relative path to the image file"));

        AddOutput(new ToolPort(PortImage, "Image", PortType.Image, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var path = _pFilePath.GetValue<string>();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return ToolResult.Failed($"Image file not found: '{path}'");
        }

        try
        {
            var mat = Cv2.ImRead(path, ImreadModes.Color);
            if (mat.Empty())
            {
                mat.Dispose();
                return ToolResult.Failed($"Failed to decode image from '{path}'");
            }

            var frame = new ImageFrame(mat, cameraId: "file", frameId: 1);
            var outputs = new Dictionary<string, object?> { [PortImage] = frame };
            return ToolResult.Ok(outputs, message: $"Loaded image {frame.Width}x{frame.Height} from '{Path.GetFileName(path)}'");
        }
        catch (Exception ex)
        {
            return ToolResult.Failed($"Error reading file '{path}': {ex.Message}", ex);
        }
    }
}
