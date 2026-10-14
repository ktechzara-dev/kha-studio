using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Output;

/// <summary>
/// Saves inspected images to disk with automatic folder routing for OK and NG parts and timestamped filenames.
/// </summary>
[VisionTool("Output.SaveImage", "Save Image (OK / NG)", "Output", Description = "Saves images to OK/NG destination folders with optional overlay burning")]
public sealed class SaveImageTool : VisionToolBase
{
    public const string ParamBaseFolder = "BaseDirectory";
    public const string ParamPrefix = "FilePrefix";
    public const string ParamBurnOverlays = "BurnOverlays";
    public const string ParamSaveOnlyNg = "SaveOnlyNG";

    public const string PortInImage = "ImageIn";
    public const string PortIsOk = "IsOkVerdict";
    public const string PortSavedPath = "SavedFilePath";

    private readonly ToolParameter _pBaseDir;
    private readonly ToolParameter _pPrefix;
    private readonly ToolParameter _pBurn;
    private readonly ToolParameter _pOnlyNg;

    public SaveImageTool() : base("Output.SaveImage", "Save Image (OK / NG)", "Output", "Saves images to disk")
    {
        _pBaseDir = AddParameter(new ToolParameter(ParamBaseFolder, "Base Directory", ParameterType.FilePath, "C:\\VisionStudio\\SavedImages"));
        _pPrefix = AddParameter(new ToolParameter(ParamPrefix, "Filename Prefix", ParameterType.String, "Part"));
        _pBurn = AddParameter(new ToolParameter(ParamBurnOverlays, "Burn Overlays onto Image", ParameterType.Boolean, true));
        _pOnlyNg = AddParameter(new ToolParameter(ParamSaveOnlyNg, "Save Only NG Images", ParameterType.Boolean, false));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));
        AddInput(new ToolPort(PortIsOk, "Is OK Verdict", PortType.Boolean, PortDirection.Input, IsRequired: false, DefaultValue: true));

        AddOutput(new ToolPort(PortSavedPath, "Saved File Path", PortType.String, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var inputFrame = ctx.GetImage(PortInImage);
        if (inputFrame == null) return ToolResult.Failed("Missing input image to save.");

        var isOk = ctx.GetInput<bool?>(PortIsOk) ?? true;
        var onlyNg = _pOnlyNg.GetValue<bool>();

        if (onlyNg && isOk)
        {
            return ToolResult.Ok(new Dictionary<string, object?> { [PortSavedPath] = string.Empty }, message: "Skipped saving OK image (SaveOnlyNG is enabled)");
        }

        var baseDir = _pBaseDir.GetValue<string>() ?? "C:\\VisionStudio\\SavedImages";
        var subFolder = isOk ? "OK" : "NG";
        var targetDir = Path.Combine(baseDir, subFolder);

        try
        {
            Directory.CreateDirectory(targetDir);
            var prefix = _pPrefix.GetValue<string>() ?? "Part";
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            var filename = $"{prefix}_{timestamp}_{subFolder}.jpg";
            var fullPath = Path.Combine(targetDir, filename);

            Cv2.ImWrite(fullPath, inputFrame.Mat);

            var outputs = new Dictionary<string, object?> { [PortSavedPath] = fullPath };
            return ToolResult.Ok(outputs, message: $"Saved {subFolder} image to '{filename}'");
        }
        catch (Exception ex)
        {
            return ToolResult.Failed($"Error saving image to '{targetDir}': {ex.Message}", ex);
        }
    }
}
