using System.Collections.Concurrent;

namespace Enterprise.Utils;

public delegate void CustomEventHandler();

public class SmartStore<T>
{
    private event CustomEventHandler OnNewItem;

    private ConcurrentQueue<T> Queue { get; } = [];

    private SemaphoreSlim Semaphore { get; } = new(0, 1);

    private readonly object semaphoreLock = new();

    public SmartStore() 
    {
        OnNewItem = () =>
        {
            lock (semaphoreLock)
            {
                if (Semaphore.CurrentCount == 0)
                    Semaphore.Release();
            }
        };
    }

    public bool IsEmpty => Queue.IsEmpty;

    public async Task WaitAsync(CancellationToken token) => await Semaphore.WaitAsync(token);

    public void RegisterOnNew(CustomEventHandler predicate) => OnNewItem += predicate;

    public void Enqueue(T item)
    {
        Queue.Enqueue(item);
        OnNewItem?.Invoke();
    }

    public bool TryDequeue(out T? item)
    {
        var ok = Queue.TryDequeue(out var x);
        item = x;
        return ok;
    }

    public bool TryPeek(out T? item)
    {
        var ok = Queue.TryPeek(out var x);
        item = x;
        return ok;
    }
}
