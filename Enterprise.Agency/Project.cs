using Enterprise.MessageHub;
using Enterprise.Utils;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System.Linq.Expressions;

namespace Enterprise.Agency;

public class Project<IContract>() : MessageHub<IContract>
    where IContract : class, IAgencyContract
{
    private List<AgentInfo> Agents { get; } = [];

    private HostApplicationBuilder Builder { get; } = Host.CreateApplicationBuilder();

    private IHost? PostService { get; set; } = default;

    public Task PostTask { get; private set; } = Task.CompletedTask;

    public Task ConnectionTask { get; private set; } = Task.CompletedTask;

    public override void Dispose()
    {
        PostService?.StopAsync();
        base.Dispose();        
    }

    public static Project<IContract> Create(string baseUrl) => Create<Project<IContract>>(baseUrl);

    public Project<IContract> AddAgent<TState, THub, IAgentContract>()
            where THub : MessageHub<IAgentContract>, new()
            where IAgentContract : class, IAgencyContract
    {
        var agentType = typeof(Agent<TState, THub, IAgentContract>);
        var info = new AgentInfo(agentType.ExtractName(), agentType.AssemblyQualifiedName!, typeof(THub).Name);
        Agents.Add(info);
        return this;
    }

    public Project<IContract> Register(Expression<Func<IContract, Action>> predicate, Action action) 
    { 
        OperationByPredicate.TryAdd(GetMessage(predicate), (null, action)); 
        return this; 
    }

    public Project<IContract> Register(Expression<Func<IContract, Func<Task>>> predicate, Action action) 
    { 
        OperationByPredicate.TryAdd(GetMessage(predicate), (null, action)); 
        return this; 
    }

    public Project<IContract> Register<TReceived>(Expression<Func<IContract, Action<TReceived>>> predicate, Action<TReceived> action) 
    { 
        OperationByPredicate.TryAdd(GetMessage(predicate), (typeof(TReceived), action)); 
        return this; 
    }

    public Project<IContract> Register<TReceived>(Expression<Func<IContract, Func<TReceived, Task>>> predicate, Action<TReceived> action) 
    { 
        OperationByPredicate.TryAdd(GetMessage(predicate), (typeof(TReceived), action)); 
        return this; 
    }

    public Project<IContract> Register<TResponse>(Expression<Func<IContract, Func<TResponse>>> predicate, Func<TResponse> action)
    {
        OperationByPredicate.TryAdd(GetMessage(predicate), (null, action));
        return this;
    }

    public Project<IContract> Register<TResponse>(Expression<Func<IContract, Func<Task<TResponse>>>> predicate, Func<Task<TResponse>> action)
    {
        OperationByPredicate.TryAdd(GetMessage(predicate), (null, action));
        return this;
    }

    public Project<IContract> Register<TReceived, TResponse>(Expression<Func<IContract, Func<TReceived, TResponse>>> predicate, Func<TReceived, TResponse> action)
    {
        OperationByPredicate.TryAdd(GetMessage(predicate), (typeof(TReceived), action));
        return this;
    }

    public Project<IContract> Register<TReceived, TResponse>(Expression<Func<IContract, Func<TReceived, Task<TResponse>>>> predicate, Func<TReceived, Task<TResponse>> action)
    {
        OperationByPredicate.TryAdd(GetMessage(predicate), (typeof(TReceived), action));
        return this;
    }

    public Project<IContract> ReceiveLogs(Action<string, string, string> action)
    {
        Me = Addresses.Logger;
        Connection.On(PostingHub.ReceiveLog, action);
        return this;
    }

    public Project<IContract> Run()
    {
        var equipment = new Equipment(Me, Queue, Connection)
        {
            ServiceDiscovery = Agents.Any()? new(true, AgentsDiscoveryAsync()) : new()
        };

        Builder.Services.AddHostedService<Post>()
                        .AddSingleton(equipment);

        PostService = Builder.Build();
        
        PostTask = PostService.RunAsync(Cancellation.Token);
        
        ConnectionTask = InitializeConnectionAsync(Cancellation.Token);

        return this;
    }

    public Task GetRunningTask() => Task.WhenAll(PostTask, ConnectionTask);

    public async Task EstablishConnectionAsync(CancellationToken cancellation = new())
    {
        await MessageHub.Post.EstablishConnectionAsync(Connection, cancellation);
    }

    public async Task ConnectToAsync(string target, CancellationToken cancellation = new())
    {
        await MessageHub.Post.ConnectToAsync(cancellation, Connection, Me, target, null);
    }

    protected Func<Task> AgentsDiscoveryAsync()
    {
        string? target = null;
        string? agentsRegistrationRequest = typeof(IAgencyContract).GetMethods()
            .FirstOrDefault(x => x.Name == nameof(IAgencyContract.AgentsRegistrationRequest))?.ToString();

        return async () =>
        {
            if (target is null)
            {
                var counter = 0;
                var requestId = Guid.NewGuid().ToString();
                var timeout = new CancellationTokenSource();
                timeout.CancelAfter(TimeSpans.ConnectionTimeout);
                var timerReconnection = new PeriodicTimer(TimeSpans.ActorConnectionAttemptPeriod);
                var id = await MessageHub.Post.EstablishConnectionAsync(Connection, Cancellation.Token).ConfigureAwait(false);

                CallbacksById.TryAdd(requestId, (string responseParcel) =>
                {
                    if (responseParcel is not null)
                    {
                        var package = JsonConvert.DeserializeObject<ManagerResponse>(responseParcel);
                        if (package is not null)
                        {
                            target = package.ManagerAddress;
                            timerReconnection.Dispose();
                        }
                    }
                });

                do
                {
                    if (++counter % 10 == 0)
                        LogPost($"Struggling to connect to Manager, attempt {++counter}");

                    var package = JsonConvert.SerializeObject(new AgentsToHire(Agents, Me), new JsonSerializerSettings
                    {
                        Formatting = Formatting.Indented
                    });

                    await Connection.SendAsync(nameof(PostingHub.SendMessage), 
                        Me, id, null, agentsRegistrationRequest, requestId, package).ConfigureAwait(false);

                    await timerReconnection.WaitForNextTickAsync().ConfigureAwait(false);
                }
                while (!Cancellation.Token.IsCancellationRequested && !timeout.Token.IsCancellationRequested && target is null);

                if (timeout.Token.IsCancellationRequested)
                    LogPost("Agents Discovery failed: no response from Manager");
            }
            else
            {
                var cancellation = new CancellationTokenSource();
                cancellation.CancelAfter(TimeSpans.ActorConnectionAttemptPeriod*50);

                await MessageHub.Post.ConnectToAsync(cancellation.Token, Connection, Me, Addresses.Central, target);

                if (cancellation.IsCancellationRequested)
                {
                    target = null;
                    await AgentsDiscoveryAsync()();
                }
            }
        };
    }
}
