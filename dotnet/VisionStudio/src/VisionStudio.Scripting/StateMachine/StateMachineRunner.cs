using System.Diagnostics;
using Microsoft.Extensions.Logging;
using VisionStudio.Core.Expressions;
using VisionStudio.Scripting.Host;

namespace VisionStudio.Scripting.StateMachine;

public sealed record StateTransitionRecord(
    DateTimeOffset Timestamp,
    string FromState,
    string ToState,
    string Reason);

/// <summary>
/// Runtime execution engine for state machine cell sequences.
/// Runs in both Desktop App and Headless Runtime.
/// </summary>
public sealed class StateMachineRunner
{
    private readonly StateMachineDefinition _definition;
    private readonly ScriptHost _scriptHost;
    private readonly ILogger? _logger;
    private readonly Stopwatch _stateTimer = new();

    private readonly List<StateTransitionRecord> _history = new();
    private readonly Dictionary<string, object?> _variables = new(StringComparer.OrdinalIgnoreCase);

    // External action delegates
    public Func<string, Task<bool>>? PipelineRunner { get; set; }
    public Action<string, object?>? TagWriter { get; set; }
    public Func<string, object?>? TagReader { get; set; }
    public Func<string, string?, Task<bool>>? RobotCommander { get; set; }

    public StateNode? CurrentState { get; private set; }
    public bool IsRunning { get; private set; }
    public IReadOnlyList<StateTransitionRecord> History => _history;
    public IDictionary<string, object?> Variables => _variables;

    public event Action<StateNode, StateNode, string>? StateChanged;

    public StateMachineRunner(
        StateMachineDefinition definition,
        ScriptHost? scriptHost = null,
        ILogger? logger = null)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _scriptHost = scriptHost ?? new ScriptHost();
        _logger = logger;

        foreach (var (k, v) in _definition.Variables)
        {
            _variables[k] = v;
        }
    }

    public void Start()
    {
        var init = _definition.InitialState;
        if (init == null)
        {
            throw new InvalidOperationException("Cannot start State Machine: No initial state defined.");
        }

        IsRunning = true;
        _history.Clear();

        CurrentState = init;
        _stateTimer.Restart();

        _logger?.LogInformation("State Machine started at initial state '{State}'", init.Name);
        _history.Add(new StateTransitionRecord(DateTimeOffset.UtcNow, "[*]", init.Name, "Start"));

        ExecuteActions(init.OnEntryActions);
    }

    public void Stop()
    {
        IsRunning = false;
        _stateTimer.Stop();
        _logger?.LogInformation("State Machine stopped at state '{State}'", CurrentState?.Name ?? "None");
    }

    /// <summary>
    /// Evaluates event triggers and attempts to transition from the current state.
    /// </summary>
    public bool TriggerEvent(string eventName, object? payload = null)
    {
        if (!IsRunning || CurrentState == null) return false;

        if (payload != null)
        {
            _variables["EventPayload"] = payload;
        }

        var matchingTransitions = _definition.Transitions
            .Where(t => t.FromStateId.Equals(CurrentState.Id, StringComparison.OrdinalIgnoreCase) &&
                        (string.IsNullOrEmpty(t.TriggerEvent) || t.TriggerEvent.Equals(eventName, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(t => t.Priority)
            .ToList();

        foreach (var tr in matchingTransitions)
        {
            if (CheckCondition(tr))
            {
                PerformTransition(tr, $"Event '{eventName}'");
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Periodic step checking state timeout and continuous condition-only transitions.
    /// </summary>
    public bool Step()
    {
        if (!IsRunning || CurrentState == null) return false;

        // 1. Check state timeout
        if (CurrentState.TimeoutMs.HasValue && !string.IsNullOrEmpty(CurrentState.TimeoutTargetStateId))
        {
            if (_stateTimer.ElapsedMilliseconds >= CurrentState.TimeoutMs.Value)
            {
                var timeoutTarget = _definition.States.FirstOrDefault(s => s.Id.Equals(CurrentState.TimeoutTargetStateId, StringComparison.OrdinalIgnoreCase));
                if (timeoutTarget != null)
                {
                    _logger?.LogWarning("State '{State}' timed out after {Elapsed} ms. Moving to '{Target}'",
                        CurrentState.Name, _stateTimer.ElapsedMilliseconds, timeoutTarget.Name);

                    PerformDirectTransition(timeoutTarget, $"Timeout ({CurrentState.TimeoutMs} ms)");
                    return true;
                }
            }
        }

        // 2. Evaluate continuous condition-only transitions
        var condTransitions = _definition.Transitions
            .Where(t => t.FromStateId.Equals(CurrentState.Id, StringComparison.OrdinalIgnoreCase) &&
                        string.IsNullOrEmpty(t.TriggerEvent))
            .OrderByDescending(t => t.Priority)
            .ToList();

        foreach (var tr in condTransitions)
        {
            if (CheckCondition(tr))
            {
                PerformTransition(tr, "Condition Met");
                return true;
            }
        }

        return false;
    }

    private bool CheckCondition(StateTransition tr)
    {
        // Safe Expression condition
        if (!string.IsNullOrEmpty(tr.ConditionExpression))
        {
            bool passed = ExpressionEvaluator.EvaluateBoolean(tr.ConditionExpression, _variables, false);
            if (!passed) return false;
        }

        // Script condition
        if (!string.IsNullOrEmpty(tr.ConditionScript))
        {
            var scriptCtx = new ScriptContext(_variables);
            var exec = _scriptHost.ExecuteAsync(tr.ConditionScript, scriptCtx, timeoutMs: 1000).GetAwaiter().GetResult();
            if (!exec.Success || exec.ReturnValue is not true)
            {
                return false;
            }
        }

        return true;
    }

    private void PerformTransition(StateTransition tr, string reason)
    {
        var targetState = _definition.States.FirstOrDefault(s => s.Id.Equals(tr.ToStateId, StringComparison.OrdinalIgnoreCase));
        if (targetState == null) return;

        var prev = CurrentState!;

        // 1. Exit current state
        ExecuteActions(prev.OnExitActions);

        // 2. Transition actions
        ExecuteActions(tr.Actions);

        // 3. Move to new state
        CurrentState = targetState;
        _stateTimer.Restart();

        _history.Add(new StateTransitionRecord(DateTimeOffset.UtcNow, prev.Name, targetState.Name, reason));
        _logger?.LogInformation("State Machine Transition: {From} ➜ {To} ({Reason})", prev.Name, targetState.Name, reason);
        StateChanged?.Invoke(prev, targetState, reason);

        // 4. Enter new state
        ExecuteActions(targetState.OnEntryActions);

        if (targetState.IsFinal)
        {
            _logger?.LogInformation("State Machine reached final state '{State}'.", targetState.Name);
            IsRunning = false;
        }
    }

    private void PerformDirectTransition(StateNode targetState, string reason)
    {
        var prev = CurrentState!;

        ExecuteActions(prev.OnExitActions);

        CurrentState = targetState;
        _stateTimer.Restart();

        _history.Add(new StateTransitionRecord(DateTimeOffset.UtcNow, prev.Name, targetState.Name, reason));
        _logger?.LogInformation("State Machine Direct Transition: {From} ➜ {To} ({Reason})", prev.Name, targetState.Name, reason);
        StateChanged?.Invoke(prev, targetState, reason);

        ExecuteActions(targetState.OnEntryActions);

        if (targetState.IsFinal)
        {
            IsRunning = false;
        }
    }

    private void ExecuteActions(IEnumerable<StateActionDefinition> actions)
    {
        foreach (var act in actions)
        {
            try
            {
                switch (act.Type)
                {
                    case StateActionType.Log:
                        _logger?.LogInformation("[StateMachine Action] {Msg}", act.Message);
                        break;

                    case StateActionType.SetTag:
                        if (!string.IsNullOrEmpty(act.TagName))
                        {
                            _variables[act.TagName] = act.TagValue;
                            TagWriter?.Invoke(act.TagName, act.TagValue);
                        }
                        break;

                    case StateActionType.Wait:
                        if (act.DelayMs > 0)
                        {
                            Thread.Sleep(act.DelayMs);
                        }
                        break;

                    case StateActionType.RunPipeline:
                        if (!string.IsNullOrEmpty(act.PipelineId) && PipelineRunner != null)
                        {
                            PipelineRunner(act.PipelineId).GetAwaiter().GetResult();
                        }
                        break;

                    case StateActionType.SendRobotCommand:
                        if (!string.IsNullOrEmpty(act.RobotCommand) && RobotCommander != null)
                        {
                            RobotCommander(act.RobotCommand, act.RobotPayload).GetAwaiter().GetResult();
                        }
                        break;

                    case StateActionType.Script:
                        if (!string.IsNullOrEmpty(act.ScriptCode))
                        {
                            var ctx = new ScriptContext(_variables, tagWriter: TagWriter);
                            _scriptHost.ExecuteAsync(act.ScriptCode, ctx, timeoutMs: 2000).GetAwaiter().GetResult();
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to execute state action {Type}: {Error}", act.Type, ex.Message);
            }
        }
    }
}
