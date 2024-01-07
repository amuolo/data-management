using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using System.Threading.Channels;

namespace Agency;

public class ManagerHub<IContract> : Hub<IContract> where IContract : class
{
}

public class Manager<IContract> : BackgroundService where IContract : class
{
    ChannelReader<Action<IContract>> ChannelReader { get; }
    ChannelWriter<Action<IContract>> ChannelWriter { get; }

    IHubContext<ManagerHub<IContract>, IContract> HubContext { get; }

    SemaphoreSlim Semaphore { get; } = new(1, 1);

    public Manager(ChannelReader<Action<IContract>> channelReader, ChannelWriter<Action<IContract>> channelWriter, IHubContext<ManagerHub<IContract>, IContract> messagingHub)
    {
        ChannelReader = channelReader;
        ChannelWriter = channelWriter;
        HubContext = messagingHub;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromMilliseconds(500));
        while (!cancellationToken.IsCancellationRequested && await timer.WaitForNextTickAsync(cancellationToken))
        {
            await foreach (var item in ChannelReader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    await Semaphore.WaitAsync(cancellationToken);
                    item(HubContext.Clients.All);
                    Semaphore.Release();
                }
                catch (Exception e)
                {
                    // TODO: finish
                    //channelWriter.TryWrite(new Log(DateTime.Now, null, e.Message));
                }
            }
        }
    }
}
