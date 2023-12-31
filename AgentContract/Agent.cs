﻿using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;

namespace Agency;

public class Agent<TState, THub, IContract> : BackgroundService 
    where TState : new()
    where THub : Hub<IContract>
    where IContract : class
{
    private IHubContext<THub, IContract> HubContext { get; }

    [Inject] public NavigationManager Navigation { get; }

    public Agent(IHubContext<THub, IContract> messagingHub)
    {
        HubContext = messagingHub;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var connection = new HubConnectionBuilder().WithUrl(new Navigator().ToAbsoluteUri(Navigator.SignalRAddress)).Build();

        foreach (var method in typeof(IContract).GetMethods())
        {
            var handle = typeof(THub).GetMethod("Handle" + method.Name);
            if (handle != null)
            {
                var parameters = method.GetParameters().Select(x => x.ParameterType).ToArray();
                var on = connection.GetType().GetMethod("On", parameters);
                on?.Invoke(connection, new object[] { method.Name, handle });
            }
        }

        await connection.StartAsync();
    }
}
