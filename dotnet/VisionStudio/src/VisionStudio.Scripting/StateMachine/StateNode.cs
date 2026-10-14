namespace VisionStudio.Scripting.StateMachine;

/// <summary>
/// A state node in the cell sequence state machine.
/// </summary>
public sealed class StateNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "State";
    public bool IsInitial { get; set; }
    public bool IsFinal { get; set; }
    public bool IsError { get; set; }

    /// <summary>
    /// Automatic state timeout in milliseconds.
    /// If in this state longer than TimeoutMs, transitions to TimeoutTargetStateId.
    /// </summary>
    public double? TimeoutMs { get; set; }
    public string? TimeoutTargetStateId { get; set; }

    public double X { get; set; }
    public double Y { get; set; }

    public List<StateActionDefinition> OnEntryActions { get; set; } = new();
    public List<StateActionDefinition> OnExitActions { get; set; } = new();

    public override string ToString() => Name;
}
