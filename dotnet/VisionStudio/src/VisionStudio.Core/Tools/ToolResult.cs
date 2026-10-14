using VisionStudio.Core.Models;
using VisionStudio.Core.Overlays;

namespace VisionStudio.Core.Tools;

/// <summary>
/// Execution status code returned by an individual vision tool.
/// </summary>
public enum ToolStatus
{
    Ok,
    Warning,
    Error,
    Skipped
}

/// <summary>
/// Immutable execution result produced by a vision tool execution step.
/// </summary>
public sealed class ToolResult : IDisposable
{
    public IReadOnlyDictionary<string, object?> Outputs { get; }
    public IReadOnlyList<IOverlayPrimitive> Overlays { get; }
    public TimeSpan ExecutionTime { get; }
    public ToolStatus Status { get; }
    public string? Message { get; }
    public Exception? Error { get; }

    private bool _isDisposed;

    public ToolResult(
        IReadOnlyDictionary<string, object?> outputs,
        IReadOnlyList<IOverlayPrimitive>? overlays = null,
        TimeSpan? executionTime = null,
        ToolStatus status = ToolStatus.Ok,
        string? message = null,
        Exception? error = null)
    {
        Outputs = outputs ?? new Dictionary<string, object?>();
        Overlays = overlays ?? Array.Empty<IOverlayPrimitive>();
        ExecutionTime = executionTime ?? TimeSpan.Zero;
        Status = status;
        Message = message;
        Error = error;
    }

    public static ToolResult Ok(
        Dictionary<string, object?> outputs,
        IReadOnlyList<IOverlayPrimitive>? overlays = null,
        TimeSpan? executionTime = null,
        string? message = null)
        => new(outputs, overlays, executionTime, ToolStatus.Ok, message);

    public static ToolResult Warning(
        string message,
        Dictionary<string, object?>? outputs = null,
        IReadOnlyList<IOverlayPrimitive>? overlays = null,
        TimeSpan? executionTime = null)
        => new(outputs ?? new(), overlays, executionTime, ToolStatus.Warning, message);

    public static ToolResult Failed(
        string message,
        Exception? exception = null,
        TimeSpan? executionTime = null)
        => new(new Dictionary<string, object?>(), Array.Empty<IOverlayPrimitive>(), executionTime, ToolStatus.Error, message, exception);

    public static ToolResult Skipped(string reason)
        => new(new Dictionary<string, object?>(), Array.Empty<IOverlayPrimitive>(), TimeSpan.Zero, ToolStatus.Skipped, reason);

    public T? GetOutput<T>(string portId)
    {
        if (Outputs.TryGetValue(portId, out var val) && val is not null)
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

    public ImageFrame? GetImage(string portId) => GetOutput<ImageFrame>(portId);

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        foreach (var val in Outputs.Values)
        {
            if (val is IDisposable disp)
            {
                disp.Dispose();
            }
        }
    }
}
