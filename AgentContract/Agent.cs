using Job;

using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;
using System.Reflection;
using System.Text.Json;

namespace Agency;

public class Agent<TState, THub, IContract> : BackgroundService
    where TState : new()
    where THub : MessageHub<IContract>, new()
    where IContract : class
{
    private IHubContext<THub, IContract> HubContext { get; }
    private THub MessageHub { get; set; } = new();
    private bool IsInitialized { get; set; }

    private HubConnection Connection => MessageHub.Connection;
    private string? Id => MessageHub?.Id;
    private string Me => typeof(THub).Name;

    private Job<(object? Package, TState State)> Job { get; set; }

    private Dictionary<string, MethodInfo> MethodsByName { get; } 
        = typeof(THub).GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance).ToDictionary(x => x.Name);

    public Agent(IHubContext<THub, IContract> hub)
    {
        HubContext = hub;        
    }

    public override void Dispose()
    {
        base.Dispose();
        MessageHub.Dispose();
    }

    private async Task CreateAsync()
    {
        if (!IsInitialized)
        {
            await Connection.InvokeAsync(Consts.Log, Me, Id, "Creating myself");

            Job = JobFactory.New<(object? Package, TState State)>(initialState: (null, new()));

            if (MethodsByName.TryGetValue(Consts.Create, out var create))
                Job = await Job.WithStep(Consts.Create, async state =>
                {
                    var init = create.Invoke(MessageHub, null);
                    if (init is not null) state.State = await (Task<TState>)init;
                })
                .Start();

            IsInitialized = true;
        }
    }

    async Task ActionMessageReceived(string sender, string senderId, string message, string messageId, string? parcel)
    {
        if (message == Consts.Delete)
        {
            Dispose();
            return;
        }
        else if (MethodsByName.TryGetValue(message, out var method))
        {
            await CreateAsync();
            await Connection.InvokeAsync(Consts.Log, Me, Id, $"processing {message}");

            await Job.WithOptions(o => o.WithAsyncLogs(MessageHub.LogAsync))
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
                    var response = res.GetType().GetProperty("Result")?.GetValue(res);
                    var responseParcel = JsonSerializer.Serialize(response);
                    await Connection.InvokeAsync(Consts.Log, Me, Id, $"{response?.GetType().Name}");
                    if (responseParcel is null)
                        await Connection.InvokeAsync(Consts.Log, Me, Id, $"Error: response null after serialization.");
                    else
                        await Connection.SendAsync(Consts.SendResponse, Me, Id, senderId, messageId, responseParcel);
                }
            })
            .Start();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await MessageHub.InitializeConnectionAsync(cancellationToken, ActionMessageReceived);
    }
}
