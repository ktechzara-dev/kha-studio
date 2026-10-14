using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VisionStudio.Scripting.StateMachine;

/// <summary>
/// Serializable definition of an industrial cell sequence state machine.
/// Governs trigger wait, image acquisition, inspection, verdict handling, robot signaling, and error recovery.
/// </summary>
public sealed class StateMachineDefinition
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Cell Sequence State Machine";
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";

    public List<StateNode> States { get; set; } = new();
    public List<StateTransition> Transitions { get; set; } = new();
    public Dictionary<string, object?> Variables { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public StateNode? InitialState => States.FirstOrDefault(s => s.IsInitial);

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static StateMachineDefinition FromJson(string json) =>
        JsonSerializer.Deserialize<StateMachineDefinition>(json, JsonOptions)
        ?? throw new InvalidOperationException("Failed to deserialize StateMachineDefinition from JSON.");

    public void SaveToFile(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        File.WriteAllText(filePath, ToJson());
    }

    public static StateMachineDefinition LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"State machine file not found: {filePath}", filePath);
        }
        return FromJson(File.ReadAllText(filePath));
    }

    /// <summary>
    /// Exports the state machine topology into a standard Mermaid diagram for documentation.
    /// </summary>
    public string ToMermaidDiagram()
    {
        var sb = new StringBuilder();
        sb.AppendLine("stateDiagram-v2");

        var stateNameMap = States.ToDictionary(s => s.Id, s => CleanName(s.Name));

        // Initial state marker
        var init = InitialState;
        if (init != null)
        {
            sb.AppendLine($"    [*] --> {CleanName(init.Name)}");
        }

        // States styling and timeouts
        foreach (var state in States)
        {
            string cleanState = CleanName(state.Name);
            if (state.IsError)
            {
                sb.AppendLine($"    note right of {cleanState} : ⚠ ERROR STATE");
            }
            if (state.TimeoutMs.HasValue)
            {
                sb.AppendLine($"    note right of {cleanState} : Timeout {state.TimeoutMs} ms");
            }
            if (state.IsFinal)
            {
                sb.AppendLine($"    {cleanState} --> [*]");
            }
        }

        // Transitions
        foreach (var tr in Transitions)
        {
            if (stateNameMap.TryGetValue(tr.FromStateId, out var fromName) &&
                stateNameMap.TryGetValue(tr.ToStateId, out var toName))
            {
                var labelParts = new List<string>();
                if (!string.IsNullOrEmpty(tr.TriggerEvent)) labelParts.Add(tr.TriggerEvent);
                if (!string.IsNullOrEmpty(tr.ConditionExpression)) labelParts.Add($"[{tr.ConditionExpression}]");
                if (!string.IsNullOrEmpty(tr.ConditionScript)) labelParts.Add("[script]");

                string label = labelParts.Count > 0 ? $" : {string.Join(" & ", labelParts)}" : "";
                sb.AppendLine($"    {fromName} --> {toName}{label}");
            }
        }

        return sb.ToString();
    }

    private static string CleanName(string name)
    {
        var clean = new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        return string.IsNullOrEmpty(clean) ? "State" : clean;
    }
}
