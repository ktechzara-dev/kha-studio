namespace VisionStudio.Scripting.StateMachine;

/// <summary>
/// A directed transition link between two states in the state machine.
/// </summary>
public sealed class StateTransition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FromStateId { get; set; } = string.Empty;
    public string ToStateId { get; set; } = string.Empty;

    /// <summary>
    /// Event name triggering this transition (e.g. "Trigger", "GrabDone", "Pass", "Fail", "ClearError").
    /// If null or empty, transition is guarded solely by conditions or evaluated continuously.
    /// </summary>
    public string? TriggerEvent { get; set; }

    /// <summary>
    /// Safe expression condition (e.g. "PartCount > 10").
    /// </summary>
    public string? ConditionExpression { get; set; }

    /// <summary>
    /// Advanced C# script condition returning boolean.
    /// </summary>
    public string? ConditionScript { get; set; }

    public int Priority { get; set; } = 0;

    public List<StateActionDefinition> Actions { get; set; } = new();

    public override string ToString() => $"{FromStateId} ➜ {ToStateId} [Event={TriggerEvent ?? "*"}]";
}
