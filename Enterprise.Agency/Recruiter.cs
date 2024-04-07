using Enterprise.MessageHub;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;

namespace Enterprise.Agency;

public class HiringState
{
    public ConcurrentDictionary<string, (Type Agent, string Id, DateTime Time, bool Active)> DossierByAgent { get; set; } = [];

    public Dictionary<string, IHost> Hosts { get; set; } = [];
}

public interface IHiringContract
{
    /* in */
    Task AgentsDiscoveryRequest(string agent);
}

public class HiringHub : MessageHub<IHiringContract>
{
    public async Task<string> GetRequest(string agent, HiringState state)
    {
        state.DossierByAgent.TryGetValue(agent, out var info);

        // TODO: finish this

        return info.Id;
    }
}

public class Recruiter : Agent<HiringState, HiringHub, IHiringContract>
{
    public Recruiter(IHubContext<ServerHub> hubContext) : base(hubContext)
    {
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await MessageHub.InitializeConnectionAsync(cancellationToken, ActionMessageReceived);

        await Post.EstablishConnectionAsync(MessageHub.Connection);

        await Post.ConnectToServerAsync(MessageHub.Connection, MessageHub.Queue.Name, cancellationToken);

        await Post.StartMessageServiceAsync(MessageHub.Queue, MessageHub.Connection, cancellationToken);
    }
}

