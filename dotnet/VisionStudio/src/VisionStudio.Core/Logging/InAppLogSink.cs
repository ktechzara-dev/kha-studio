using System.Collections.Concurrent;

namespace VisionStudio.Core.Logging;

/// <summary>
/// Thread-safe in-memory log sink with a bounded circular buffer for in-app monitoring.
/// </summary>
public sealed class InAppLogSink : IInAppLogSink
{
    private readonly int _maxCapacity;
    private readonly ConcurrentQueue<LogEntry> _queue = new();
    private readonly object _lock = new();

    public event Action<LogEntry>? LogEmitted;

    public InAppLogSink(int maxCapacity = 2000)
    {
        _maxCapacity = maxCapacity > 0 ? maxCapacity : 1000;
    }

    public void Emit(LogEntry entry)
    {
        lock (_lock)
        {
            _queue.Enqueue(entry);
            while (_queue.Count > _maxCapacity && _queue.TryDequeue(out _))
            {
                // discard oldest
            }
        }

        LogEmitted?.Invoke(entry);
    }

    public IReadOnlyList<LogEntry> GetRecentEntries()
    {
        lock (_lock)
        {
            return _queue.ToArray();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            while (_queue.TryDequeue(out _)) { }
        }
    }
}
