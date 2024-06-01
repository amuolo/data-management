using Enterprise.MessageHub;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using System.Collections.Concurrent;

namespace Enterprise.Agency;

public class Manager : Agent<AgencyCulture, ManagerHub, IAgencyContract>
{
    private Task? OffBoardingService { get; set; }

    private Task? OnBoardingService { get; set; }

    private SemaphoreSlim OnBoardingProcess { get; set; } = new(0, 1);

    private ConcurrentQueue<(ManagerResponse Response, Func<Task> PostAction)> Tasks { get; set; } = [];

    public Manager(IHubContext<PostingHub> hubContext, AgencyCulture workplace) : base(hubContext, workplace)
    {
        MessageHub.Me = Addresses.Central;
        Job = Job.Initialize(workplace);
    }

    protected override async Task ExecuteAsync(CancellationToken token)
    {
        OffBoardingService = OffBoardingAsync(token);
        OnBoardingService = OnBoardingAsync(token);

        await MessageHub.InitializeConnectionAsync(token, ActionMessageReceived);
        var onConnect = OnConnectRequest();
        var info = new Equipment(Me, MessageHub.Queue, MessageHub.Connection);

        await Post.StartMessageServiceAsync(info, token);
    }

    protected IDisposable OnConnectRequest()
    {
        return MessageHub.Connection.On(PostingHub.ReceiveConnectRequest,
            async (string sender, string senderId, string requestId, string target) =>
            {
                if (sender != Me)
                {
                    Func<Task> postAction = target == Me
                        ? async () => await MessageHub.Connection.SendAsync(nameof(PostingHub.ConnectionEstablished), Me, MessageHub.Id, senderId, requestId)
                        : () => Task.CompletedTask;

                    await RunAgentsDiscoveryAsync(sender, postAction);
                }
            });  
    }

    private async Task RunAgentsDiscoveryAsync(string sender, Func<Task> postAction)
    {
        await Job.WithStep($"{PostingHub.ReceiveConnectRequest} from {sender}", async state =>
        {
            try
            {
                Tasks.Enqueue((await MessageHub.AgentsDiscovery(sender, state), postAction));
                if(OnBoardingProcess.CurrentCount == 0)
                    OnBoardingProcess.Release();
            }
            catch (Exception ex)
            {
                MessageHub.LogPost(ex.Message);
            }
        })
        .StartAsync();
    }

    protected async Task OnBoardingAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await OnBoardingProcess.WaitAsync(token);
            if (token.IsCancellationRequested) break;

            try
            {
                while (!Tasks.IsEmpty)
                {
                    var ok = Tasks.TryDequeue(out var task);

                    if (!ok)
                    {
                        MessageHub.LogPost("Issue with Tasks dequeuing");
                    }
                    else
                    {
                        foreach (var agent in task.Response.Hired)
                            await Post.ConnectToAsync(token, MessageHub.Connection, MessageHub.Me, agent.MessageHubName, null);
                        task.Response.Hired.Clear();
                        await task.PostAction();
                    }                   
                }
            }
            catch (Exception ex)
            {
                MessageHub.LogPost(ex.Message);
            }
        }
    }

    protected async Task OffBoardingAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var state = (await Job.GetStateAsync())!;
            var outerTimer = new PeriodicTimer(state.OffBoardingWaitingTime);

            try
            {
                if (state is null || !state.InfoByActor.Any())
                {
                    await outerTimer.WaitForNextTickAsync(token);
                    continue;
                }

                using (var innerTimer = new PeriodicTimer(GetNextDecommission(state)))
                {
                    await innerTimer.WaitForNextTickAsync(token);
                    if (token.IsCancellationRequested) break;

                    foreach (var agentInfo in GetFiredAgents(state))
                    {
                        if (state.Hosts.TryGetValue(agentInfo.Name, out var host))
                        {
                            MessageHub.PostWithResponse(
                                new HubAddress(agentInfo.MessageHubName),
                                manager => manager.DeleteRequest,
                                (Action<DeletionProcess>)(async process =>
                                {
                                    if (process.Status)
                                    {
                                        await host.StopAsync();
                                        state.Hosts.Remove(agentInfo.Name);
                                        agentInfo.Active = false;
                                    }
                                }));
                        }
                        else
                            MessageHub.LogPost($"Decommissioner failed to fire agent {agentInfo.Name}: not found.");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageHub.LogPost(ex.Message);
            }
        }
    }

    private TimeSpan GetNextDecommission(AgencyCulture state)
    {
        var items = state.InfoByActor.SelectMany(x => x.Value.Select(y => DateTime.Now - y.LastInteraction)).OrderBy(x => x).ToArray();

        var max = items.FirstOrDefault() - state.HireAgentsPeriod;

        return max.TotalSeconds < 0 ? -max : TimeSpan.FromSeconds(1);
    }

    private AgentInfo[] GetFiredAgents(AgencyCulture state)
    {
        var agents = state.InfoByActor.SelectMany(x => x.Value)
                                      .OrderBy(x => x.LastInteraction)
                                      .Where(x => (DateTime.Now - x.LastInteraction) > state.HireAgentsPeriod)
                                      .ToArray();

        return agents;
    }
}

