using Microsoft.AspNetCore.Builder;
using System.Linq.Expressions;

namespace Enterprise.Agency;

public class Office<IContract>
    : MessageHub<IContract>
    where IContract : class
{
    private List<(Type Agent, Type Hub)> Actors { get; } = [];

    private Dictionary<string, WebApplication> WebApplications { get; } = [];

    private Office() : base()
    {
    }

    public static Office<IContract> Create()
    {              
        return new Office<IContract>();
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
        Task.Run(async () =>
        {
            await InitializeConnectionAsync(TokenSource.Token);
            StartServiceHiringAgents(TokenSource.Token);
        });

        return this;
    }

    private void StartServiceHiringAgents(CancellationToken token)
    {
        Task.Run(async () =>
        {
            var timer = new PeriodicTimer(Consts.HireAgentsPeriod);

            do
            {
                PostWithResponse<object, object, string[]>(null, Consts.AgentsDiscovery, null, HireAgents);
                await timer.WaitForNextTickAsync();
            }
            while (!token.IsCancellationRequested);

            void HireAgents(string[] registeredAgents)
            {
                foreach (var actor in Actors.Where(x => !registeredAgents.Contains(x.Agent.Name)))
                {
                    WebApplications[actor.Agent.Name] = Recruitment.Recruit(actor);
                }
            }
        });
    }

    public Office<IContract> Register<TReceived>(Expression<Func<IContract, Delegate>> predicate, Action<TReceived> action)
    {
        if (!GetMessage(predicate, out var message)) return this;
        
        OperationByPredicate.TryAdd(message, (typeof(TReceived), action));

        return this;
    }

    public Office<IContract> Register(Expression<Func<IContract, Delegate>> predicate, Action action)
    {
        if (!GetMessage(predicate, out var message)) return this;

        OperationByPredicate.TryAdd(message, (null, action));

        return this;
    }
}
