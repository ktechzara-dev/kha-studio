namespace VisionStudio.Core.Logging;

/// <summary>
/// Log levels supported across VisionStudio.
/// </summary>
public enum LogLevel
{
    Verbose,
    Debug,
    Information,
    Warning,
    Error,
    Fatal
}

/// <summary>
/// Represents a structured log entry for UI display and diagnostics.
/// </summary>
public record LogEntry(
    DateTimeOffset Timestamp,
    LogLevel Level,
    string Source,
    string Message,
    string? Exception = null);

/// <summary>
/// Sink interface for receiving in-app log entries in real-time on UI/diagnostics panels.
/// </summary>
public interface IInAppLogSink
{
    /// <summary>
    /// Event fired whenever a new log entry is dispatched.
    /// </summary>
    event Action<LogEntry>? LogEmitted;

    /// <summary>
    /// Emits a log entry into the sink.
    /// </summary>
    void Emit(LogEntry entry);

    /// <summary>
    /// Gets a thread-safe snapshot of recent log entries.
    /// </summary>
    IReadOnlyList<LogEntry> GetRecentEntries();

    /// <summary>
    /// Clears cached log history.
    /// </summary>
    void Clear();
}
