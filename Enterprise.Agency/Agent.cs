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
    protected record Core
    {
        public object? Package { get; set; }
        public TState State { get; set; } = new();
    }

    protected IHubContext<PostingHub> ServerHub { get; }
    protected THub MessageHub { get; set; }
    protected bool IsInitialized { get; set; }
    protected string Me => MessageHub.Me;

    protected Job<Core> Job { get; set; } = JobFactory.New<Core>(initialState: new());

    protected Dictionary<string, MethodInfo> MethodsByName { get; } 
        = typeof(THub).GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance).ToDictionary(x => x.Name);

    public Agent(IHubContext<PostingHub> hubContext, Workplace workplace)
    {
        MessageHub = MessageHub<IContract>.Create<THub>(workplace.Url);
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

        var info = new Equipment(Me, MessageHub.Queue, MessageHub.Connection);

        await Post.StartMessageServiceAsync(info, token);
    }

    protected async Task CreateAsync()
    {
        if (!IsInitialized)
        {
            if (MethodsByName.TryGetValue(nameof(IHubContract.CreateRequest), out var create))
            {
                MessageHub.LogPost("Creating myself");
                var returnType = create.ReturnType;

                Job = await Job
                    .WithOptions(o => o.WithLogs(MessageHub.LogPost))
                    .WithStep(nameof(IHubContract.CreateRequest), async state =>
                {
                    var r = create.Invoke(MessageHub, null);
                    if (r is null)
                    {
                        MessageHub.LogPost($"null state retrieved after creation.");
                    }
                    else if (create.ReturnType == typeof(Task<TState>))
                    {
                        state.State = await (Task<TState>)r;
                    }
                    else if (create.ReturnType == typeof(TState))
                    {
                        state.State = (TState)r;
                    }
                    else
                    {
                        MessageHub.LogPost($"Incorrect Return Type method {nameof(IHubContract.CreateRequest)}");
                    }
                })
                .Start();
            }

            IsInitialized = true;
        }
    }

    protected async Task ActionMessageReceived(string sender, string senderId, string message, string messageId, string? package)
    {
        if (message == nameof(IHubContract.DeleteRequest) && sender == Addresses.Central)
        {
            Dispose();
            return;
        }
        else if (message == nameof(IHubContract.ReadRequest))
        {
            // TODO: generalizes read request response with agency contract
            // TODO: check whether this mechanism handles read requests in parallel
            await CreateAsync();
            MessageHub.LogPost($"processing {message}");
            MessageHub.Queue.Enqueue(new Parcel(sender, senderId, Job.State, nameof(IHubContract.ReadResponse)));
            return;
        }
        else if (MethodsByName.TryGetValue(message, out var method))
        {
            // TODO: consider re-using the office registration pattern instead of inventing a new one here
            await CreateAsync();
            MessageHub.LogPost($"processing {message}");

            await Job.WithOptions(o => o.WithLogs(MessageHub.LogPost))
                     .WithStep($"{message}", async state =>
            {
                var parameters = method.GetParameters().Select(p => p.ParameterType == typeof(TState) ? state.State :
                                    (package is not null ? JsonConvert.DeserializeObject(package, p.ParameterType) : null)).ToArray();

                var res = method.Invoke(MessageHub, parameters);
                var returnType = method.ReturnType;
                var err = $"Inconsistent return type found when agent {Me} processed {message}";

                // TODO: handle return types different than tasks
                if (returnType == typeof(Task))
                {
                    if (res is null)
                        throw new Exception(err);
                    await (Task)res;
                }
                else if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    if (res is null)
                        throw new Exception(err);
                    await (Task)res;
                    var result = res.GetType().GetProperty("Result")?.GetValue(res);
                    MessageHub.Queue.Enqueue(new Parcel(sender, senderId, result, message) 
                        with { Type = nameof(PostingHub.SendResponse), Id = messageId });
                }
            })
            .Start();
            return;
        }
    }
}
