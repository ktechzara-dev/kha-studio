using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Output;

/// <summary>
/// Appends inspection cycle metrics, timestamps, and verdicts to an industrial audit CSV file.
/// </summary>
[VisionTool("Output.LogToCsv", "Log To CSV", "Output", Description = "Appends inspection records, measurements, and verdicts to CSV file")]
public sealed class LogToCsvTool : VisionToolBase
{
    private static readonly object FileLock = new();

    public const string ParamFilePath = "CsvFilePath";
    public const string ParamPartId = "PartIdentifier";

    public const string PortValue1 = "Value1";
    public const string PortValue2 = "Value2";
    public const string PortVerdict = "Verdict";
    public const string PortIsLogged = "IsLogged";

    private readonly ToolParameter _pPath;
    private readonly ToolParameter _pPartId;

    public LogToCsvTool() : base("Output.LogToCsv", "Log To CSV", "Output", "Appends records to CSV file")
    {
        _pPath = AddParameter(new ToolParameter(ParamFilePath, "CSV File Path", ParameterType.FilePath, "C:\\VisionStudio\\logs\\inspection_log.csv"));
        _pPartId = AddParameter(new ToolParameter(ParamPartId, "Part Identifier", ParameterType.String, "PART-001"));

        AddInput(new ToolPort(PortValue1, "Value 1", PortType.Number, PortDirection.Input, IsRequired: false));
        AddInput(new ToolPort(PortValue2, "Value 2", PortType.Number, PortDirection.Input, IsRequired: false));
        AddInput(new ToolPort(PortVerdict, "Verdict", PortType.String, PortDirection.Input, IsRequired: false, DefaultValue: "OK"));

        AddOutput(new ToolPort(PortIsLogged, "Is Logged", PortType.Boolean, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var csvPath = _pPath.GetValue<string>() ?? "C:\\VisionStudio\\logs\\inspection_log.csv";
        var partId = _pPartId.GetValue<string>() ?? "PART";
        var val1 = ctx.GetInput<object>(PortValue1)?.ToString() ?? string.Empty;
        var val2 = ctx.GetInput<object>(PortValue2)?.ToString() ?? string.Empty;
        var verdict = ctx.GetInput<string>(PortVerdict) ?? "OK";

        try
        {
            var dir = Path.GetDirectoryName(csvPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            lock (FileLock)
            {
                var fileExists = File.Exists(csvPath);
                using var writer = new StreamWriter(csvPath, append: true);
                if (!fileExists)
                {
                    writer.WriteLine("Timestamp,CycleIndex,PartId,Value1,Value2,Verdict");
                }

                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                writer.WriteLine($"{timestamp},{ctx.ExecutionIndex},{partId},{val1},{val2},{verdict}");
            }

            var outputs = new Dictionary<string, object?> { [PortIsLogged] = true };
            return ToolResult.Ok(outputs, message: $"Logged cycle #{ctx.ExecutionIndex} to '{Path.GetFileName(csvPath)}'");
        }
        catch (Exception ex)
        {
            return ToolResult.Failed($"Failed appending to CSV: {ex.Message}", ex);
        }
    }
}
