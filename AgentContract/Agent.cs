using Job;
using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;

namespace Agency;

public class Agent<TState> : BackgroundService where TState : new()
{
    private ConcurrentQueue<IMessage> Messages { get; set; } = new();

    private Job<TState> Job { get; set; } = JobFactory.New<TState>();

    private SemaphoreSlim Semaphore { get; set; } = new(1, 1);

    public static Agent<TState> Create() => new Agent<TState>();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // TODO: finish
            await Job.WithStep("a", state => Task.Delay(1000)).Start();  
            await Task.Delay(1000, stoppingToken);
        }
    }
}
