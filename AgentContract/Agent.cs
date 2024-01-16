using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;

namespace Agency;

public class Agent<TState, THub, IContract> : BackgroundService
    where TState : new()
    where THub : Hub<IContract>
    where IContract : class, IAgencyContract
{
    private IHubContext<THub, IContract> HubContext { get; }

    private HubConnection Connection { get; set;  }

    public Agent(IHubContext<THub, IContract> messagingHub)
    {
        HubContext = messagingHub;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        Connection = new HubConnectionBuilder().WithUrl(SignalR.Url).WithAutomaticReconnect().Build();

        foreach (var method in typeof(IContract).GetMethods())
        {
            var handle = typeof(THub).GetMethod("Handle" + method.Name);
            if (handle != null)
            {
                var parameters = method.GetParameters().Select(x => x.ParameterType).ToArray();
                var on = Connection.GetType().GetMethod("On", parameters);
                on?.Invoke(Connection, new object[] { method.Name, handle });
            }
        }

        Connection.Reconnecting += (sender) =>
        {
            HubContext.Clients.All.Send("Attempting to reconnect...");
            return Task.CompletedTask;
        };

        Connection.Reconnected += (sender) =>
        {
            HubContext.Clients.All.Send("Reconnected to the server");
            return Task.CompletedTask;
        };

        Connection.Closed += (sender) =>
        {
            HubContext.Clients.All.Send("Connection Closed");
            return Task.CompletedTask;
        };

        await Connection.StartAsync();
    }
}
