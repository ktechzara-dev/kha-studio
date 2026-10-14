using Microsoft.Extensions.Logging;
using VisionStudio.Core.Models;

namespace VisionStudio.Core.Tools;

/// <summary>
/// Execution runtime environment and input provider passed to IVisionTool.Execute.
/// </summary>
public sealed class ToolContext
{
    public IReadOnlyDictionary<string, object?> Inputs { get; }
    public CancellationToken CancellationToken { get; }
    public long ExecutionIndex { get; }
    public ILogger? Logger { get; }

    public ToolContext(
        IReadOnlyDictionary<string, object?> inputs,
        CancellationToken cancellationToken = default,
        long executionIndex = 0,
        ILogger? logger = null)
    {
        Inputs = inputs ?? new Dictionary<string, object?>();
        CancellationToken = cancellationToken;
        ExecutionIndex = executionIndex;
        Logger = logger;
    }

    public T? GetInput<T>(string portId)
    {
        if (Inputs.TryGetValue(portId, out var val) && val is not null)
        {
            if (val is T typed) return typed;
            try
            {
                return (T)Convert.ChangeType(val, typeof(T));
            }
            catch
            {
                return default;
            }
        }
        return default;
    }

    public ImageFrame? GetImage(string portId) => GetInput<ImageFrame>(portId);
}
