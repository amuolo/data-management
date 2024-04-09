using Enterprise.Job;
using Enterprise.MessageHub;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using System.Reflection;
using System.Text.Json;

namespace Enterprise.Agency;

public class Agent<TState, THub, IContract> : BackgroundService
    where TState : new()
    where THub : MessageHub<IContract>, new()
    where IContract : class
{
    protected IHubContext<ServerHub> ServerHub { get; }
    protected THub MessageHub { get; set; } = new();
    protected bool IsInitialized { get; set; }

    protected Job<(object? Package, TState State)> Job { get; set; }
        = JobFactory.New<(object? Package, TState State)>(initialState: (null, new()));

    private Dictionary<string, MethodInfo> MethodsByName { get; } 
        = typeof(THub).GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance).ToDictionary(x => x.Name);

    public Agent(IHubContext<ServerHub> hubContext)
    {
        ServerHub = hubContext;        
    }

    public override void Dispose()
    {
        MessageHub.LogPost($"disposing");
        base.Dispose();
        MessageHub.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await MessageHub.InitializeConnectionAsync(cancellationToken, ActionMessageReceived);

        await Post.StartMessageServiceAsync(MessageHub.Queue, MessageHub.Connection, cancellationToken);
    }

    private async Task CreateAsync()
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

    protected async Task ActionMessageReceived(string sender, string senderId, string message, string messageId, string? parcel)
    {
        if (message == Messages.ReadRequest)
        {
            MessageHub.LogPost($"processing {message}");
            MessageHub.Queue.Enqueue(new Parcel(senderId, Job.State, Messages.ReadResponse));
            // TODO: check whether this mechanism handles read requests in parallel
        }
        else if (message.Contains(Messages.ConnectTo) && message.Contains(typeof(THub).Name))
        {
            MessageHub.LogPost($"processing {message}");
            MessageHub.Queue.Enqueue(new Parcel(senderId, null, Messages.Connected));
        }
        else if (MethodsByName.TryGetValue(message, out var method))
        {
            await CreateAsync();
            MessageHub.LogPost($"processing {message}");

            await Job.WithOptions(o => o.WithLogs(MessageHub.LogPost))
                     .WithStep($"{message}", async state =>
            {
                var parameters = method.GetParameters().Select(p => p.ParameterType == typeof(TState) ? state.State :
                                    (parcel is not null ? JsonSerializer.Deserialize(parcel, p.ParameterType) : null)).ToArray();

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
                    MessageHub.Queue.Enqueue(new Parcel(senderId, result, message) with { Type = MessageTypes.SendResponse });
                }
            })
            .Start();
        }
    }
}
