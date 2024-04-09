using Enterprise.MessageHub;
using Enterprise.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Linq.Expressions;

namespace Enterprise.Agency;

public class Office<IContract> : MessageHub<IContract>
    where IContract : class
{
    public bool IsReady { get; private set; }

    private List<(Type Agent, Type Hub)> Actors { get; } = [];

    private Dictionary<string, IHost> Hosts { get; } = [];

    private HostApplicationBuilder Builder { get; } = Host.CreateApplicationBuilder();

    private Office() : base()
    {
    }

    public static Office<IContract> Create()
    {              
        var office = new Office<IContract>();

        office.Builder.Services.AddHostedService<Post>()
                               .AddSingleton(office.Queue)
                               .AddSingleton(office.Connection);

        return office;
    }

    public Office<IContract> AddAgent<TState, THub, IHubContract>()
            where TState : new()
            where THub : MessageHub<IHubContract>, new()
            where IHubContract : class
    {
        Actors.Add((typeof(Agent<TState, THub, IHubContract>), typeof(THub)));
        return this;
    }

    public Office<IContract> Run()
    {
        var host = Builder.Build();
        host.RunAsync(Cancellation.Token);

        Task.Run(async () =>
        {
            await InitializeConnectionAsync(Cancellation.Token).ConfigureAwait(false);
            await MessageHub.Post.ConnectToActorAsync(Connection, Queue.Name, Addresses.Server, Cancellation.Token);
            StartServiceHiringAgents(Cancellation.Token);
        });

        return this;
    }

    private void StartServiceHiringAgents(CancellationToken token)
    {
        Task.Run(async () =>
        {
            var timer = new PeriodicTimer(TimeSpans.HireAgentsPeriod);

            do
            {
                PostWithResponse<object, object, string[]>(null, Messages.AgentsDiscovery, null, HireAgents);
                await timer.WaitForNextTickAsync().ConfigureAwait(false);
            }
            while (!token.IsCancellationRequested);

            // TODO: move recruitment to server-side to avoid over-production of agents
            void HireAgents(string[] registeredAgents)
            {
                foreach (var actor in Actors.Where(x => !registeredAgents.Contains(x.Agent.Name)))
                {
                    LogPost($"Recruiting {TypeHelper.ExtractName(actor.Agent)}");
                    Hosts[actor.Agent.Name] = Recruitment.Recruit(actor);
                }
            }
        });
    }

    public Office<IContract> Register<TReceived>(Expression<Func<IContract, Delegate>> predicate, Action<TReceived> action)
    {       
        OperationByPredicate.TryAdd(GetMessage(predicate), (typeof(TReceived), action));
        return this;
    }

    public Office<IContract> Register(Expression<Func<IContract, Delegate>> predicate, Action action)
    {
        OperationByPredicate.TryAdd(GetMessage(predicate), (null, action));
        return this;
    }
}
