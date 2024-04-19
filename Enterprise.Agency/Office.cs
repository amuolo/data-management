using Enterprise.MessageHub;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Linq.Expressions;

namespace Enterprise.Agency;

public class Office<IContract>() : MessageHub<IContract>
    where IContract : class, IAgencyContract
{
    private List<Curriculum> Agents { get; } = [];

    private HostApplicationBuilder Builder { get; } = Host.CreateApplicationBuilder();

    private IHost? PostService { get; set; } = default;

    public Task PostTask { get; private set; } = Task.CompletedTask;

    public Task ConnectionTask { get; private set; } = Task.CompletedTask;

    public override void Dispose()
    {
        PostService?.StopAsync();
        base.Dispose();
    }

    public static Office<IContract> Create(string baseUrl) => Create<Office<IContract>>(baseUrl);

    public Office<IContract> AddAgent<TState, THub, IAgentContract>()
            where TState : new()
            where THub : MessageHub<IAgentContract>, new()
            where IAgentContract : class, IHubContract
    {
        var agentType = typeof(Agent<TState, THub, IAgentContract>);
        var info = new Curriculum(typeof(THub).Name, agentType.AssemblyQualifiedName!);
        Agents.Add(info);
        return this;
    }

    public Office<IContract> Register(Expression<Func<IContract, Action>> predicate, Action action) 
    { 
        OperationByPredicate.TryAdd(GetMessage(predicate), (null, action)); 
        return this; 
    }
    public Office<IContract> Register(Expression<Func<IContract, Func<Task>>> predicate, Action action) 
    { 
        OperationByPredicate.TryAdd(GetMessage(predicate), (null, action)); 
        return this; 
    }
    public Office<IContract> Register<TReceived>(Expression<Func<IContract, Action<TReceived>>> predicate, Action<TReceived> action) 
    { 
        OperationByPredicate.TryAdd(GetMessage(predicate), (typeof(TReceived), action)); 
        return this; 
    }
    public Office<IContract> Register<TReceived>(Expression<Func<IContract, Func<TReceived, Task>>> predicate, Action<TReceived> action) 
    { 
        OperationByPredicate.TryAdd(GetMessage(predicate), (typeof(TReceived), action)); 
        return this; 
    }

    public Office<IContract> ReceiveLogs(Action<string, string, string> action)
    {
        Me = Addresses.Logger;
        Connection.On(nameof(IHubContract.ReceiveLog), action);
        return this;
    }

    public Office<IContract> Run()
    {
        Builder.Services.AddHostedService<Post>()
                        .AddSingleton(new Equipment(Me, Queue, Connection));

        PostService = Builder.Build();
        
        PostTask = PostService.RunAsync(Cancellation.Token);
        
        ConnectionTask = InitializeConnectionAsync(Cancellation.Token);

        PostWithResponse(
            Addresses.Central, 
            office => office.AgentsRegistrationRequest, 
            new AgentsDossier(Agents, Me),
            (Action<ManagerResponse>)(response =>
            {
                if (!response.Status)
                    LogPost($"Initial Agents Discovery request failed.");
            }));

        return this;
    }

    public Task GetRunningTask() => Task.WhenAll(PostTask, ConnectionTask);
}
