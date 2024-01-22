using Job;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;
using System.Reflection;

namespace Agency;

public class Agent<TState, THub, IContract> : BackgroundService
    where TState : new()
    where THub : MessageHub<IContract>, new()
    where IContract : class
{
    private IHubContext<THub, IContract> HubContext { get; }
    private HubConnection Connection { get; set; }
    private THub MessageHub { get; set; }
    private bool IsInitialized { get; set; }

    private Job<(object? Package, TState State)> Job { get; set; } 

    public Agent(IHubContext<THub, IContract> hub)
    {
        HubContext = hub;
        MessageHub = new();
        // TODO: use options to pass the logger and the progresses
        Job = JobFactory.New<(object? Package, TState State)>(initialState: (null, new()));
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        Connection = new HubConnectionBuilder().WithUrl(Contract.Url).WithAutomaticReconnect().Build();

        var methods = typeof(THub).GetMethods().Where(x => x.Name.Contains("Handle") || x.Name == Contract.Create).ToDictionary(x => x.Name);
        
        Connection.On<string, Guid, string, object?>(Contract.ReceiveMessage, async (sender, senderId, message, package) =>
        {
            if(methods.TryGetValue("Handle" + message, out var method))
            {
                if(!IsInitialized)
                {
                    await Connection.InvokeAsync(Contract.SendMessage, typeof(THub).Name, MessageHub.Id, "Creating myself", null);
                    
                    if (methods.TryGetValue(Contract.Create, out var create))
                        await Job.WithStep(Contract.Create, async state =>
                        {
                            var init = create.Invoke(MessageHub, null);
                            if (init is not null) state.State = await (Task<TState>)init;
                        })
                        .Start();

                    IsInitialized = true;
                }
                await Connection.InvokeAsync(Contract.SendMessage, typeof(THub).Name, MessageHub.Id, $"processing {message}", null);
                await Job.WithStep($"{message}", s => method.Invoke(MessageHub, [s.Package, s.State])).Start();
            }
        });
        
        Connection.Reconnecting += (sender) => Connection.InvokeAsync(Contract.SendMessage, typeof(THub).Name, MessageHub.Id, "Attempting to reconnect...", null);
        Connection.Reconnected += (sender) => Connection.InvokeAsync(Contract.SendMessage, typeof(THub).Name, MessageHub.Id, "Reconnected to the server", null);
        Connection.Closed += (sender) => Connection.InvokeAsync(Contract.SendMessage, typeof(THub).Name, MessageHub.Id, "Connection Closed", null);
        
        await Connection.StartAsync(cancellationToken);        
    }
}
