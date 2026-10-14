using Serilog.Core;
using Serilog.Events;
using VisionStudio.Core.Logging;
using CoreLogLevel = VisionStudio.Core.Logging.LogLevel;

namespace VisionStudio.App.Logging;

/// <summary>
/// Bridges Serilog log events to the VisionStudio in-app log sink for real-time UI display.
/// </summary>
public sealed class SerilogInAppSink : ILogEventSink
{
    private readonly IInAppLogSink _innerSink;

    public SerilogInAppSink(IInAppLogSink innerSink)
    {
        _innerSink = innerSink ?? throw new ArgumentNullException(nameof(innerSink));
    }

    public void Emit(LogEvent logEvent)
    {
        var level = logEvent.Level switch
        {
            LogEventLevel.Verbose => CoreLogLevel.Verbose,
            LogEventLevel.Debug => CoreLogLevel.Debug,
            LogEventLevel.Information => CoreLogLevel.Information,
            LogEventLevel.Warning => CoreLogLevel.Warning,
            LogEventLevel.Error => CoreLogLevel.Error,
            LogEventLevel.Fatal => CoreLogLevel.Fatal,
            _ => CoreLogLevel.Information
        };

        var source = logEvent.Properties.TryGetValue("SourceContext", out var prop)
            ? prop.ToString().Trim('\"')
            : "VisionStudio";

        var message = logEvent.RenderMessage();
        var exception = logEvent.Exception?.ToString();

        _innerSink.Emit(new LogEntry(
            Timestamp: logEvent.Timestamp,
            Level: level,
            Source: source,
            Message: message,
            Exception: exception));
    }
}
