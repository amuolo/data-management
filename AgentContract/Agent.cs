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
    private bool IsConnected => MessageHub?.IsConnected?? false;
    private string? Id => MessageHub?.Id;
    private string Me => typeof(THub).Name;

    private Job<(object? Package, TState State)> Job { get; set; }

    private Dictionary<string, MethodInfo> MethodsByName { get; } = typeof(THub).GetMethods()
                                                                    .Where(x => x.Name.Contains("Handle") || x.Name == Contract.Create)
                                                                    .ToDictionary(x => x.Name);

    public Agent(IHubContext<THub, IContract> hub)
    {
        HubContext = hub;
        
        // TODO: use options to pass the logger and the progresses       
    }

    private async Task CreateAsync()
    {
        if (!IsInitialized)
        {
            await Connection.InvokeAsync(Contract.Log, Me, Id, "Creating myself");

            Job = JobFactory.New<(object? Package, TState State)>(initialState: (null, new()));

            if (MethodsByName.TryGetValue(Contract.Create, out var create))
                await Job.WithStep(Contract.Create, async state =>
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
        if (MethodsByName.TryGetValue("Handle" + message, out var method))
        {
            await CreateAsync();
            await Connection.InvokeAsync(Contract.Log, Me, Id, $"processing {message}");

            await Job.WithStep($"{message}", async state =>
            {
                var parameters = method.GetParameters().Select(p =>
                    p.ParameterType == typeof(TState) ? state.State : Deserialize(parcel, p.ParameterType)).ToArray();

                var res = method.Invoke(MessageHub, parameters);

                if (res is not null && method.ReturnType == typeof(Task))
                {
                    await (Task)res;
                }
                else if (res is not null && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    await (Task)res;
                    var response = res.GetType().GetProperty("Result")?.GetValue(res);
                    await Connection.SendAsync(Contract.SendResponse, Me, Id, senderId, messageId, response);
                }
            })
            .Start();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await MessageHub.InitializeConnectionAsync(cancellationToken, ActionMessageReceived);
    }

    private object? Deserialize(string? parcel, Type type)
    {
        if (parcel is null) return parcel;
        return typeof(JsonSerializer)?.GetMethod(nameof(JsonSerializer.DeserializeAsync), [type])?.Invoke(null, [parcel]);
    }
}
