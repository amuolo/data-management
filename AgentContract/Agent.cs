using Job;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;

namespace Agency;

public class Agent<TState, THub, IContract> : BackgroundService 
    where TState : new()
    where THub : Hub<IContract>
    where IContract : class
{
    private NavigationManager NavigationManager { get; set; }

    private IHubContext<THub, IContract> HubContext { get; }

    private Job<TState> Job { get; set; } = JobFactory.New<TState>();

    private SemaphoreSlim Semaphore { get; set; } = new(1, 1);

    public TState? State => Job.State;

    public Agent(IHubContext<THub, IContract> messagingHub)
    {
        HubContext = messagingHub;
        NavigationManager = new Navigator("http://localhost:8000/application", "http://localhost:8000/application");
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var connection = new HubConnectionBuilder().WithUrl(NavigationManager.ToAbsoluteUri("/signalr-messaging")).Build();

        foreach (var method in typeof(IContract).GetMethods())
        {
            var handle = typeof(THub).GetMethod("Handle" + method.Name);
            if (handle != null)
            {
                var parameters = method.GetParameters().Select(x => x.ParameterType).ToArray();
                var on = connection.GetType().GetMethod("On", parameters);
                on?.Invoke(connection, new object[] { method.Name, handle });
            }
        }

        await connection.StartAsync();

        //using PeriodicTimer timer = new(TimeSpan.FromMilliseconds(500));
        //while (!cancellationToken.IsCancellationRequested && await timer.WaitForNextTickAsync(cancellationToken))
        //{
        //    await foreach (var item in channelReader.ReadAllAsync(cancellationToken))
        //    {
        //        try
        //        {
        //            // TODO: finish
        //            await Semaphore.WaitAsync(cancellationToken);
        //            await Job.WithStep("a", state => Task.Delay(1000)).Start(cancellationToken);
        //            Semaphore.Release();
        //        }
        //        catch (Exception e)
        //        {
        //            channelWriter.TryWrite(new Log(DateTime.Now, null, e.Message));
        //        }
        //    }
        //}
    }
}
