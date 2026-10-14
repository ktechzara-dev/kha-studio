using VisionStudio.Scripting.StateMachine;

namespace VisionStudio.Scripting.Tests;

public sealed class StateMachineTests
{
    [Fact]
    public void StateMachine_ExecutesCellInspectionSequence()
    {
        // Define an industrial inspection state machine
        var def = new StateMachineDefinition
        {
            Name = "Vision Cell Sequence"
        };

        var stateIdle = new StateNode { Id = "S_Idle", Name = "Idle", IsInitial = true };
        var stateWaitTrigger = new StateNode { Id = "S_WaitTrigger", Name = "WaitTrigger" };
        var stateGrab = new StateNode { Id = "S_Grab", Name = "GrabImage" };
        var stateInspect = new StateNode { Id = "S_Inspect", Name = "InspectPart" };
        var statePass = new StateNode { Id = "S_Pass", Name = "HandlePass" };
        var stateFail = new StateNode { Id = "S_Fail", Name = "HandleFail", IsError = true };

        // Add action to statePass: set tag "RejectCylinder" = false
        statePass.OnEntryActions.Add(new StateActionDefinition
        {
            Type = StateActionType.SetTag,
            TagName = "RejectCylinder",
            TagValue = false
        });

        // Add action to stateFail: set tag "RejectCylinder" = true
        stateFail.OnEntryActions.Add(new StateActionDefinition
        {
            Type = StateActionType.SetTag,
            TagName = "RejectCylinder",
            TagValue = true
        });

        def.States.AddRange(new[] { stateIdle, stateWaitTrigger, stateGrab, stateInspect, statePass, stateFail });

        // Transitions
        def.Transitions.Add(new StateTransition { FromStateId = "S_Idle", ToStateId = "S_WaitTrigger", TriggerEvent = "Start" });
        def.Transitions.Add(new StateTransition { FromStateId = "S_WaitTrigger", ToStateId = "S_Grab", TriggerEvent = "PartPresent" });
        def.Transitions.Add(new StateTransition { FromStateId = "S_Grab", ToStateId = "S_Inspect", TriggerEvent = "FrameReady" });

        // Inspection branch: score >= 0.8 -> Pass, score < 0.8 -> Fail
        def.Transitions.Add(new StateTransition
        {
            FromStateId = "S_Inspect",
            ToStateId = "S_Pass",
            TriggerEvent = "InspectDone",
            ConditionExpression = "Score >= 0.8"
        });
        def.Transitions.Add(new StateTransition
        {
            FromStateId = "S_Inspect",
            ToStateId = "S_Fail",
            TriggerEvent = "InspectDone",
            ConditionExpression = "Score < 0.8"
        });

        def.Transitions.Add(new StateTransition { FromStateId = "S_Pass", ToStateId = "S_Idle", TriggerEvent = "Reset" });
        def.Transitions.Add(new StateTransition { FromStateId = "S_Fail", ToStateId = "S_Idle", TriggerEvent = "ClearFault" });

        // Runner execution
        var runner = new StateMachineRunner(def);
        runner.Start();

        runner.CurrentState!.Name.Should().Be("Idle");

        // Event: Start -> WaitTrigger
        runner.TriggerEvent("Start").Should().BeTrue();
        runner.CurrentState.Name.Should().Be("WaitTrigger");

        // Event: PartPresent -> GrabImage
        runner.TriggerEvent("PartPresent").Should().BeTrue();
        runner.CurrentState.Name.Should().Be("GrabImage");

        // Event: FrameReady -> InspectPart
        runner.TriggerEvent("FrameReady").Should().BeTrue();
        runner.CurrentState.Name.Should().Be("InspectPart");

        // Set Score = 0.95 and trigger InspectDone -> should enter HandlePass!
        runner.Variables["Score"] = 0.95;
        runner.TriggerEvent("InspectDone").Should().BeTrue();
        runner.CurrentState.Name.Should().Be("HandlePass");
        runner.Variables["RejectCylinder"].Should().Be(false);

        // Reset to Idle
        runner.TriggerEvent("Reset").Should().BeTrue();
        runner.CurrentState.Name.Should().Be("Idle");
        runner.History.Should().HaveCountGreaterThan(5);
    }

    [Fact]
    public void StateMachine_TransitionsOnStateTimeout()
    {
        var def = new StateMachineDefinition();
        var s1 = new StateNode
        {
            Id = "S1",
            Name = "WaitingPart",
            IsInitial = true,
            TimeoutMs = 50,
            TimeoutTargetStateId = "S_Timeout"
        };
        var sTimeout = new StateNode
        {
            Id = "S_Timeout",
            Name = "TimeoutFault",
            IsError = true
        };

        def.States.Add(s1);
        def.States.Add(sTimeout);

        var runner = new StateMachineRunner(def);
        runner.Start();
        runner.CurrentState!.Name.Should().Be("WaitingPart");

        // Wait beyond timeout
        Thread.Sleep(70);

        // Step runner
        bool stepped = runner.Step();
        stepped.Should().BeTrue();
        runner.CurrentState.Name.Should().Be("TimeoutFault");
    }

    [Fact]
    public void ToMermaidDiagram_GeneratesValidFlowchartDefinition()
    {
        var def = new StateMachineDefinition { Name = "Cell Workflow" };
        def.States.Add(new StateNode { Id = "S1", Name = "Ready", IsInitial = true });
        def.States.Add(new StateNode { Id = "S2", Name = "Inspecting" });
        def.States.Add(new StateNode { Id = "S3", Name = "Error", IsError = true });

        def.Transitions.Add(new StateTransition { FromStateId = "S1", ToStateId = "S2", TriggerEvent = "Trigger" });
        def.Transitions.Add(new StateTransition { FromStateId = "S2", ToStateId = "S3", TriggerEvent = "Fault" });

        string mermaid = def.ToMermaidDiagram();

        mermaid.Should().Contain("stateDiagram-v2");
        mermaid.Should().Contain("[*] --> Ready");
        mermaid.Should().Contain("Ready --> Inspecting : Trigger");
        mermaid.Should().Contain("Inspecting --> Error : Fault");
        mermaid.Should().Contain("note right of Error : ⚠ ERROR STATE");
    }
}
