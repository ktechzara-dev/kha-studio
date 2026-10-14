using VisionStudio.App.Services;

namespace VisionStudio.App.Tests;

public class UndoRedoManagerTests
{
    private sealed class MockCommand : IUndoableCommand
    {
        public string Description { get; }
        public int ExecuteCount { get; private set; }
        public int UnexecuteCount { get; private set; }

        public MockCommand(string description) => Description = description;

        public void Execute() => ExecuteCount++;
        public void Unexecute() => UnexecuteCount++;
    }

    [Fact]
    public void Execute_PushesToUndoStack_AndClearsRedoStack()
    {
        var manager = new UndoRedoManager();
        var cmd1 = new MockCommand("Cmd 1");
        var cmd2 = new MockCommand("Cmd 2");

        manager.Execute(cmd1);
        cmd1.ExecuteCount.Should().Be(1);
        manager.CanUndo.Should().BeTrue();
        manager.CanRedo.Should().BeFalse();
        manager.NextUndoDescription.Should().Be("Cmd 1");

        manager.Execute(cmd2);
        cmd2.ExecuteCount.Should().Be(1);
        manager.NextUndoDescription.Should().Be("Cmd 2");

        manager.Undo();
        cmd2.UnexecuteCount.Should().Be(1);
        manager.CanRedo.Should().BeTrue();
        manager.NextRedoDescription.Should().Be("Cmd 2");

        manager.Redo();
        cmd2.ExecuteCount.Should().Be(2);
        manager.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void Clear_EmptiesBothStacks()
    {
        var manager = new UndoRedoManager();
        var cmd = new MockCommand("Test");
        manager.Execute(cmd);
        manager.Undo();

        manager.Clear();
        manager.CanUndo.Should().BeFalse();
        manager.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void MaxHistory_LimitsUndoStackSize()
    {
        var manager = new UndoRedoManager(maxHistory: 3);
        manager.Execute(new MockCommand("1"));
        manager.Execute(new MockCommand("2"));
        manager.Execute(new MockCommand("3"));
        manager.Execute(new MockCommand("4"));

        // Only 3 should remain
        manager.Undo();
        manager.Undo();
        manager.Undo();
        manager.CanUndo.Should().BeFalse();
    }
}
