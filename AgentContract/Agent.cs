using Job;
using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;

namespace Agency;

public abstract class Agent<TState> : BackgroundService where TState : new()
{
    private ConcurrentQueue<IMessage> Messages { get; set; } = new();

    private Job<TState> Job { get; set; } = JobFactory.New<TState>();

    private SemaphoreSlim Semaphore { get; set; } = new(1, 1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromMilliseconds(500));
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            // TODO: finish
            await Semaphore.WaitAsync(stoppingToken);
            await Job.WithStep("a", state => Task.Delay(1000)).Start(stoppingToken);  
            await Task.Delay(1000, stoppingToken);
            Semaphore.Release();
        }
    }
}
