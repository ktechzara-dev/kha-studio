using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Acquisition;

/// <summary>
/// Sequentially loads image files from a designated directory to simulate camera streams or playback recorded batches.
/// </summary>
[VisionTool("Acquisition.FolderSequence", "Folder Sequence", "Acquisition", Description = "Plays back a sequence of images from a folder")]
public sealed class FolderSequenceTool : VisionToolBase
{
    public const string ParamFolderPath = "FolderPath";
    public const string ParamLoop = "Loop";
    public const string ParamSearchPattern = "SearchPattern";

    public const string PortImage = "Image";
    public const string PortFileName = "FileName";
    public const string PortIndex = "Index";

    private readonly ToolParameter _pFolderPath;
    private readonly ToolParameter _pLoop;
    private readonly ToolParameter _pSearchPattern;

    private int _currentIndex;
    private string[]? _cachedFiles;
    private string? _lastFolder;

    public FolderSequenceTool() : base("Acquisition.FolderSequence", "Folder Sequence", "Acquisition", "Loads sequence of images from directory")
    {
        _pFolderPath = AddParameter(new ToolParameter(ParamFolderPath, "Folder Path", ParameterType.FilePath, string.Empty));
        _pLoop = AddParameter(new ToolParameter(ParamLoop, "Loop Sequence", ParameterType.Boolean, true));
        _pSearchPattern = AddParameter(new ToolParameter(ParamSearchPattern, "Search Pattern", ParameterType.String, "*.png|*.jpg|*.jpeg|*.bmp"));

        AddOutput(new ToolPort(PortImage, "Image", PortType.Image, PortDirection.Output));
        AddOutput(new ToolPort(PortFileName, "File Name", PortType.String, PortDirection.Output));
        AddOutput(new ToolPort(PortIndex, "Index", PortType.Number, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var folder = _pFolderPath.GetValue<string>();
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return ToolResult.Failed($"Directory does not exist: '{folder}'");
        }

        if (_cachedFiles == null || _lastFolder != folder)
        {
            var extensions = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff" };
            _cachedFiles = Directory.GetFiles(folder)
                .Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => f)
                .ToArray();
            _lastFolder = folder;
            _currentIndex = 0;
        }

        if (_cachedFiles.Length == 0)
        {
            return ToolResult.Failed($"No supported images found in directory '{folder}'");
        }

        var loop = _pLoop.GetValue<bool>();
        if (_currentIndex >= _cachedFiles.Length)
        {
            if (loop)
            {
                _currentIndex = 0;
            }
            else
            {
                return ToolResult.Skipped("Reached end of folder sequence.");
            }
        }

        var filePath = _cachedFiles[_currentIndex];
        var mat = Cv2.ImRead(filePath, ImreadModes.Color);
        if (mat.Empty())
        {
            mat.Dispose();
            return ToolResult.Failed($"Failed to decode image from '{filePath}'");
        }

        var frame = new ImageFrame(mat, cameraId: "folder-sim", frameId: _currentIndex);
        var outputs = new Dictionary<string, object?>
        {
            [PortImage] = frame,
            [PortFileName] = Path.GetFileName(filePath),
            [PortIndex] = _currentIndex
        };

        _currentIndex++;
        return ToolResult.Ok(outputs, message: $"Loaded frame #{frame.FrameId} ({Path.GetFileName(filePath)})");
    }

    public override void Reset()
    {
        _currentIndex = 0;
        _cachedFiles = null;
    }
}
