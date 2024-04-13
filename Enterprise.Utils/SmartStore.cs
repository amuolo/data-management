using System.Collections.Concurrent;

namespace Enterprise.Utils;

public delegate void CustomEventHandler();

public class SmartStore<T>
{
    public event CustomEventHandler? OnNewItem;

    private ConcurrentQueue<T> Queue { get; } = [];

    public SemaphoreSlim Semaphore { get; } = new(0, 1);

    public SmartStore() 
    {
        OnNewItem += () =>
        {
            if (Semaphore.CurrentCount == 0)
                Semaphore.Release();
        };
    }

    public bool IsEmpty => Queue.IsEmpty;

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
