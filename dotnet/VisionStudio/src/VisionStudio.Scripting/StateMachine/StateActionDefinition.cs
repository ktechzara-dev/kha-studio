namespace VisionStudio.Scripting.StateMachine;

public enum StateActionType
{
    RunPipeline,
    SetTag,
    SendRobotCommand,
    Wait,
    Log,
    Script
}

/// <summary>
/// Serializable action executed during state entry, exit, or transition.
/// </summary>
public sealed class StateActionDefinition
{
    public StateActionType Type { get; set; } = StateActionType.Log;

    // For RunPipeline
    public string? PipelineId { get; set; }

    // For SetTag
    public string? TagName { get; set; }
    public object? TagValue { get; set; }

    // For SendRobotCommand
    public string? RobotCommand { get; set; }
    public string? RobotPayload { get; set; }

    // For Wait
    public int DelayMs { get; set; } = 100;

    // For Log
    public string LogLevel { get; set; } = "Info";
    public string Message { get; set; } = string.Empty;

    // For Script
    public string? ScriptCode { get; set; }

    public override string ToString() => $"{Type}: {Message ?? TagName ?? RobotCommand ?? PipelineId}";
}
