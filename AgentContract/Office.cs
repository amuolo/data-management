﻿using Microsoft.AspNetCore.Builder;
using System.Linq.Expressions;

namespace Agency;

public class Office<IContract>
    : MessageHub<IContract>
    where IContract : class
{
    private List<(Type Agent, Type Hub)> Actors { get; } = [];

    private CancellationTokenSource TokenSource { get; } = new();

    private Dictionary<string, WebApplication> WebApplications { get; } = [];

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
            await EstablishConnectionWithServerAsync();
            await HireAgentsAsync();
        });

        return this;
    }

    private async Task EstablishConnectionWithServerAsync()
    {
        var connected = false;
        var timerReconnection = new PeriodicTimer(Consts.ReconnectionTimeOut);

        do
        {
            PostWithResponse<object, object, ServerInfo>(null, Consts.ConnectToServer, null, _ => connected = true);
            await timerReconnection.WaitForNextTickAsync();
        }
        while (!connected && !TokenSource.IsCancellationRequested);
    }

    private async Task HireAgentsAsync()
    {
        var timer = new PeriodicTimer(Consts.TimeOut);

        do
        {
            PostWithResponse<object, object, string[]>(null, Consts.AgentsDiscovery, null, HireAgents);
            await timer.WaitForNextTickAsync();
        }
        while (!IsConnected && !TokenSource.IsCancellationRequested);

        void HireAgents(string[] registeredAgents)
        {
            foreach (var actor in Actors.Where(x => !registeredAgents.Contains(x.Agent.Name)))
            {
                WebApplications[actor.Agent.Name] = Recruitment.Recruit(actor);
            }
        }
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
