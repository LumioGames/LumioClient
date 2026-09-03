using System.Collections.Concurrent;

namespace Lumio.Client.Bot.Host;

/// <summary>
/// Console hosts have no <see cref="SynchronizationContext"/>. <c>await Task.Delay</c> would
/// resume on the thread pool and break <c>WorldManager</c> owner-thread checks. This pump
/// keeps Delay continuations on the thread that called <see cref="Run"/>.
/// </summary>
internal sealed class BotHostOwnerPump : SynchronizationContext, IDisposable
{
    private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = new();

    public static int Run(Func<Task<int>> work)
    {
        using var pump = new BotHostOwnerPump();
        SynchronizationContext? previous = Current;
        SetSynchronizationContext(pump);
        try
        {
            Task<int> task = work();
            pump.RunUntilCompleted(task);
            return task.GetAwaiter().GetResult();
        }
        finally
        {
            SetSynchronizationContext(previous);
        }
    }

    public override void Post(SendOrPostCallback d, object? state)
    {
        _queue.Add((d, state));
    }

    public override void Send(SendOrPostCallback d, object? state)
    {
        if (ReferenceEquals(Current, this))
        {
            d(state);
            return;
        }

        using var done = new ManualResetEventSlim(false);
        Exception? error = null;
        Post(
            _ =>
            {
                try
                {
                    d(state);
                }
                catch (Exception exception)
                {
                    error = exception;
                }
                finally
                {
                    done.Set();
                }
            },
            null);
        done.Wait();
        if (error is not null)
        {
            throw error;
        }
    }

    public override SynchronizationContext CreateCopy() => this;

    private void RunUntilCompleted(Task task)
    {
        while (!task.IsCompleted)
        {
            if (_queue.TryTake(out (SendOrPostCallback Callback, object? State) item, 50))
            {
                item.Callback(item.State);
            }
        }

        while (_queue.TryTake(out (SendOrPostCallback Callback, object? State) leftover))
        {
            leftover.Callback(leftover.State);
        }
    }

    public void Dispose()
    {
        _queue.CompleteAdding();
        _queue.Dispose();
    }
}
