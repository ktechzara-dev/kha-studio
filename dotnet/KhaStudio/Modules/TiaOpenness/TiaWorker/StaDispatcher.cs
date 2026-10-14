using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace KhaStudio.TiaOpenness;

/// <summary>
/// Luồng STA chuyên dụng để thực thi mọi lệnh Siemens Openness API/COM an toàn,
/// không gây nghẽn UI và đảm bảo ApartmentState.STA bắt buộc của Siemens.
/// </summary>
public sealed class StaDispatcher : IDisposable
{
    private readonly BlockingCollection<Action> _queue = new();
    private readonly Thread _thread;
    private bool _disposed;

    public StaDispatcher()
    {
        _thread = new Thread(RunLoop)
        {
            IsBackground = true,
            Name = "KhaStudio-TiaOpenness-STA"
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    private void RunLoop()
    {
        while (!_queue.IsCompleted)
        {
            try
            {
                if (_queue.TryTake(out var action, Timeout.Infinite))
                {
                    action();
                }
            }
            catch (InvalidOperationException)
            {
                break; // Hàng đợi đã hoàn tất
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[StaDispatcher Error] {ex.Message}");
            }
        }
    }

    public Task<T> InvokeAsync<T>(Func<T> func)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(StaDispatcher));

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Add(() =>
        {
            try
            {
                var result = func();
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }

    public Task InvokeAsync(Action action)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(StaDispatcher));

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Add(() =>
        {
            try
            {
                action();
                tcs.SetResult(true);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _queue.CompleteAdding();
        if (_thread.IsAlive)
        {
            _thread.Join(1500);
        }
        _queue.Dispose();
    }
}
