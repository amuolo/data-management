using Enterprise.MessageHub;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using System.Collections.Concurrent;

namespace Enterprise.Agency;

public class Manager : Agent<Workplace, ManagerHub, IAgencyContract>
{
    private Task? OffBoardingService { get; set; }

    private Task? OnBoardingService { get; set; }

    private SemaphoreSlim OnBoardingProcess { get; set; } = new(0, 1);

    private ConcurrentQueue<(ManagerResponse Response, Func<Task> PostAction)> Tasks { get; set; } = [];

    public Manager(IHubContext<PostingHub> hubContext, Workplace workplace) : base(hubContext, workplace)
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
                Func<Task> postAction = target == Me 
                    ? async () => await MessageHub.Connection.SendAsync(nameof(PostingHub.ConnectionEstablished), Me, MessageHub.Id, senderId, requestId) 
                    : () => Task.CompletedTask;

                await RunAgentsDiscoveryAsync(sender, postAction);
            });  
    }

    private async Task RunAgentsDiscoveryAsync(string sender, Func<Task> postAction)
    {
        await Job.WithStep($"{PostingHub.ReceiveConnectRequest}", async state =>
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
        .Start();
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
                            await Post.ConnectToAsync(MessageHub.Connection, MessageHub.Me, agent.Name, default);
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
                if (state is null || !state.DossierByActor.Any())
                {
                    await outerTimer.WaitForNextTickAsync(token);
                    continue;
                }

                using (var innerTimer = new PeriodicTimer(GetNextDecommission(state)))
                {
                    await innerTimer.WaitForNextTickAsync(token);
                    if (token.IsCancellationRequested) break;

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
            catch (Exception ex)
            {
                MessageHub.LogPost(ex.Message);
            }
        }
    }

    private TimeSpan GetNextDecommission(Workplace state)
    {
        var items = state.DossierByActor.SelectMany(x => x.Value.Select(y => DateTime.Now - y.Time)).OrderBy(x => x).ToArray();

        var max = items.FirstOrDefault() - state.HireAgentsPeriod;

        return max.TotalSeconds < 0 ? -max : TimeSpan.FromSeconds(1);
    }

    private string[] GetFiredAgents(Workplace state)
    {
        var agents = state.DossierByActor.SelectMany(x => x.Value)
                                         .OrderBy(x => x.Time)
                                         .Where(x => (DateTime.Now - x.Time) > state.HireAgentsPeriod)
                                         .Select(x => x.AgentInfo.Name)
                                         .ToArray();

        return agents;
    }
}

