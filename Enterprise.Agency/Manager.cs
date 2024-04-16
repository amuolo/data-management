using Enterprise.MessageHub;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;

namespace Enterprise.Agency;

public class ManagerState
{
    public ConcurrentDictionary<string, List<(AgentInfo AgentInfo, DateTime Time, bool Active)>> DossierByActor { get; set; } = [];

    public Dictionary<string, IHost> Hosts { get; set; } = [];

    public Type[] AgentTypes { get; set; } = [];
}

public class ManagerHub : MessageHub<IAgencyContract>
{
    public async Task<ManagerResponse> AgentsDiscoveryRequest(AgentsDossier dossier, ManagerState state)
    {
        var hired = new List<AgentInfo>();

        if (dossier.Agents is not null)
        {
            var agents = dossier.Agents.Where(x => x is not null).ToList()!;

            if (state.DossierByActor.TryGetValue(dossier.From, out var archive))
            {
                agents.ForEach(agent =>
                {
                    if (!archive.Any(x => x.AgentInfo.Name == agent.Name))
                    {
                        archive.Add((agent, DateTime.Now, true));
                        hired.Add(agent);
                    }
                });
            }
            else
            {
                state.DossierByActor[dossier.From] = agents.Select(x => (x, DateTime.Now, true)).ToList();
                hired = agents;
            }
        }
        else
        {
            LogPost($"dossier agents found null.");
        }

        await Recruitment.RecruitAsync(hired, this, state.Hosts, state.AgentTypes);
        return new ManagerResponse(true);
    }

    public async Task<ManagerResponse> AgentsDiscovery(string from, ManagerState state)
    {
        var hired = new List<AgentInfo>();

        if (state.DossierByActor.TryGetValue(from, out var archive))
        {
            archive.ForEach(x =>
            {
                if (!x.Active)
                    hired.Add(x.AgentInfo);
                x.Active = true;
                x.Time = DateTime.Now;
            });
        }

        await Recruitment.RecruitAsync(hired, this, state.Hosts, state.AgentTypes);
        return new ManagerResponse(true);
    }
}

public class Manager : Agent<ManagerState, ManagerHub, IAgencyContract>
{
    private Task? DecommissionService { get; set; }

    public Manager(IHubContext<PostingHub> hubContext, Type[] agents) : base(hubContext)
    {
        MessageHub.Me = Addresses.Central;
        Job.WithStep("", state => state.State.AgentTypes = agents).Start();
    }

    protected override async Task ExecuteAsync(CancellationToken token)
    {
        DecommissionService = DecommissionAsync(token);

        await MessageHub.InitializeConnectionAsync(token, ManagerActionMessageReceived);

        var registration = RegisterActionConnectRequest();

        var info = new ActorInfo(Me, MessageHub.Queue, MessageHub.Connection);

        await Post.StartMessageServiceAsync(info, token);
    }

    protected IDisposable RegisterActionConnectRequest()
    {
        return MessageHub.Connection.On(nameof(IHubContract.ConnectRequest),
            async (string sender, string senderId, string requestId, string target) =>
            {
                await RunAgentsDiscoveryAsync(nameof(IHubContract.ConnectRequest), sender);
                if (target == Me)
                    await MessageHub.Connection.SendAsync(nameof(IHubContract.ConnectionEstablished), MessageHub.Id, senderId, requestId);
            });  
    }

    protected async Task ManagerActionMessageReceived(string sender, string senderId, string message, string messageId, string? package)
    {
        await ActionMessageReceived(sender, senderId, message, messageId, package);
        if (sender != Me)
            await RunAgentsDiscoveryAsync(message, sender);
    }

    private async Task RunAgentsDiscoveryAsync(string message, string sender)
    {
        await Job.WithStep($"{message}", async state =>
        {
            try
            {
                var managerResponse = await MessageHub.AgentsDiscovery(sender, state.State);
            }
            catch (Exception ex)
            {
                MessageHub.LogPost(ex.Message);
            }
        })
        .Start();
    }

    protected async Task DecommissionAsync(CancellationToken token)
    {
        var state = Job.State.State;
        var outerTimer = new PeriodicTimer(TimeSpan.FromSeconds(10));

        while (!token.IsCancellationRequested)
        {
            if (state is null || !state.DossierByActor.Any())
            {
                await outerTimer.WaitForNextTickAsync();
                continue;
            }

            using(var innerTimer = new PeriodicTimer(GetNextDecommission(state)))
            {
                await innerTimer.WaitForNextTickAsync();
                foreach (var agent in GetFiredAgents(state))
                {
                    if (state.Hosts.TryGetValue(agent, out var host))
                    {
                        MessageHub.PostWithResponse(
                            agent,
                            manager => manager.DeleteRequest,
                            (Action<DeletionProcess>)(async process =>
                            {
                                if (process.Status)
                                    await host.StopAsync();
                            }));
                    }
                    else
                        MessageHub.LogPost($"Decommissioner failed to fire agent {agent}: not found.");
                }
            }
        }
    }

    private TimeSpan GetNextDecommission(ManagerState state)
    {
        var items = state.DossierByActor.SelectMany(x => x.Value.Select(y => DateTime.Now - y.Time)).OrderBy(x => x).ToArray();

        var max = items.FirstOrDefault() - TimeSpans.HireAgentsPeriod;

        return max.TotalSeconds < 0 ? -max : TimeSpan.FromSeconds(1);
    }

    private string[] GetFiredAgents(ManagerState state)
    {
        var agents = state.DossierByActor.SelectMany(x => x.Value)
                                         .OrderBy(x => x.Time)
                                         .Where(x => (DateTime.Now - x.Time) > TimeSpans.HireAgentsPeriod)
                                         .Select(x => x.AgentInfo.Name)
                                         .ToArray();

        return agents;
    }
}

