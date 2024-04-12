﻿using Enterprise.Job;
using Enterprise.MessageHub;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System.Reflection;

namespace Enterprise.Agency;

public class Agent<TState, THub, IContract> : BackgroundService
    where TState : new()
    where THub : MessageHub<IContract>, new()
    where IContract : class, IHubContract
{
    protected IHubContext<ServerHub> ServerHub { get; }
    protected THub MessageHub { get; set; } = new();
    protected bool IsInitialized { get; set; }
    protected string Me => MessageHub.Me;

    protected Job<(object? Package, TState State)> Job { get; set; }
        = JobFactory.New<(object? Package, TState State)>(initialState: (null, new()));

    protected Dictionary<string, MethodInfo> MethodsByName { get; } 
        = typeof(THub).GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance).ToDictionary(x => x.Name);

    public Agent(IHubContext<ServerHub> hubContext)
    {
        ServerHub = hubContext;        
    }

    public override void Dispose()
    {
        MessageHub.LogPost($"disposing");
        MessageHub.Dispose();
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken token)
    {
        await MessageHub.InitializeConnectionAsync(token, ActionMessageReceived);

        var info = new ActorInfo(Me, MessageHub.Queue, MessageHub.Connection);

        await Post.StartMessageServiceAsync(info, token);
    }

    protected async Task CreateAsync()
    {
        if (!IsInitialized)
        {
            MessageHub.LogPost("Creating myself");

            if (MethodsByName.TryGetValue(Messages.Create, out var create))
                Job = await Job.WithStep(Messages.Create, async state =>
                {
                    var init = create.Invoke(MessageHub, null);
                    if (init is not null) state.State = await (Task<TState>)init;
                })
                .Start();

            IsInitialized = true;
        }
    }

    protected async Task ActionMessageReceived(string sender, string senderId, string message, string messageId, string? package)
    {
        if (message == Messages.Delete && sender.Contains(nameof(ManagerHub)))
        {
            Dispose();
            return;
        }
        else if (message == Messages.ReadRequest)
        {
            // TODO: generalizes read request response with agency contract
            // TODO: check whether this mechanism handles read requests in parallel
            await CreateAsync();
            MessageHub.LogPost($"processing {message}");
            MessageHub.Queue.Enqueue(new Parcel(sender, senderId, Job.State, Messages.ReadResponse));
            return;
        }
        else if (MethodsByName.TryGetValue(message, out var method))
        {
            await CreateAsync();
            MessageHub.LogPost($"processing {message}");

            await Job.WithOptions(o => o.WithLogs(MessageHub.LogPost))
                     .WithStep($"{message}", async state =>
            {
                var parameters = method.GetParameters().Select(p => p.ParameterType == typeof(TState) ? state.State :
                                    (package is not null ? JsonConvert.DeserializeObject(package, p.ParameterType) : null)).ToArray();

                var res = method.Invoke(MessageHub, parameters);

                if (res is null) return;

                if (method.ReturnType == typeof(Task))
                {
                    await (Task)res;
                }
                else if (method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    await (Task)res;
                    var result = res.GetType().GetProperty("Result")?.GetValue(res);
                    MessageHub.Queue.Enqueue(new Parcel(sender, senderId, result, message) 
                        with { Type = nameof(Enterprise.MessageHub.ServerHub.SendResponse), Id = messageId });
                }
            })
            .Start();
            return;
        }
    }
}