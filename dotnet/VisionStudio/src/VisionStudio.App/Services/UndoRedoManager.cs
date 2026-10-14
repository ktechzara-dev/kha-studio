namespace VisionStudio.App.Services;

/// <summary>
/// Interface for reversible user actions on the pipeline canvas.
/// </summary>
public interface IUndoableCommand
{
    string Description { get; }
    void Execute();
    void Unexecute();
}

/// <summary>
/// Standard Undo/Redo history stack manager.
/// </summary>
public sealed class UndoRedoManager
{
    private readonly Stack<IUndoableCommand> _undoStack = new();
    private readonly Stack<IUndoableCommand> _redoStack = new();
    private readonly int _maxHistory;

    public event EventHandler? StateChanged;

    public UndoRedoManager(int maxHistory = 100)
    {
        _maxHistory = maxHistory;
    }

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public string? NextUndoDescription => CanUndo ? _undoStack.Peek().Description : null;
    public string? NextRedoDescription => CanRedo ? _redoStack.Peek().Description : null;

    /// <summary>
    /// Executes a command and adds it to the undo stack, clearing the redo stack.
    /// </summary>
    public void Execute(IUndoableCommand command)
    {
        command.Execute();
        _undoStack.Push(command);
        _redoStack.Clear();

        if (_undoStack.Count > _maxHistory)
        {
            // Trim oldest item
            var items = _undoStack.ToArray();
            _undoStack.Clear();
            for (int i = items.Length - 2; i >= 0; i--)
            {
                _undoStack.Push(items[i]);
            }
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Undoes the last command.
    /// </summary>
    public void Undo()
    {
        if (!CanUndo) return;

        var cmd = _undoStack.Pop();
        cmd.Unexecute();
        _redoStack.Push(cmd);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Redoes the previously undone command.
    /// </summary>
    public void Redo()
    {
        if (!CanRedo) return;

        var cmd = _redoStack.Pop();
        cmd.Execute();
        _undoStack.Push(cmd);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Clears both undo and redo histories.
    /// </summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
