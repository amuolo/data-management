using Enterprise.MessageHub;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace Enterprise.Agency;

public class Manager : Agent<Workplace, ManagerHub, IAgencyContract>
{
    private Task? OffBoardingService { get; set; }

    private Task? OnBoardingService { get; set; }

    private SemaphoreSlim OnBoardingProcess { get; set; } = new(0, 1);

    private ManagerResponse ManagerResponse { get; set; }

    private Func<Task> PostAction { get; set; } = () => Task.CompletedTask;

    public Manager(IHubContext<PostingHub> hubContext, Workplace workplace) : base(hubContext, workplace)
    {
        MessageHub.Me = Addresses.Central;
        Job = Job.Initialize((null, workplace));
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
        return MessageHub.Connection.On(nameof(PostingHub.ConnectRequest),
            async (string sender, string senderId, string requestId, string target) =>
            {
                await RunAgentsDiscoveryAsync(nameof(PostingHub.ConnectRequest), sender);
                if (target == Me)
                    PostAction = async () => await MessageHub.Connection.SendAsync(nameof(PostingHub.ConnectionEstablished), Me, MessageHub.Id, senderId, requestId);
            });  
    }

    private async Task RunAgentsDiscoveryAsync(string message, string sender)
    {
        await Job.WithStep($"{message}", async state =>
        {
            try
            {
                ManagerResponse = await MessageHub.AgentsDiscovery(sender, state.State);
                OnBoardingProcess.Release();
                await PostAction();
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

            try
            {
                foreach (var agent in ManagerResponse.Hired)
                {
                    await Post.ConnectToAsync(MessageHub.Connection, MessageHub.Me, MessageHub.Id, agent.Name, default);
                }
                ManagerResponse.Hired.Clear();
            }
            catch (Exception ex)
            {
                MessageHub.LogPost(ex.Message);
            }
        }
    }


    protected async Task OffBoardingAsync(CancellationToken token)
    {
        var state = Job.State.State;
        var outerTimer = new PeriodicTimer(state.OffBoardingWaitingTime);

        while (!token.IsCancellationRequested)
        {
            try
            {
                if (state is null || !state.DossierByActor.Any())
                {
                    await outerTimer.WaitForNextTickAsync();
                    continue;
                }

                using (var innerTimer = new PeriodicTimer(GetNextDecommission(state)))
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

