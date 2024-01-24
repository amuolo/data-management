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

    private async Task InitializeAsync()
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

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {     
        Connection.On<string, string, string, string, string?>(Contract.ReceiveMessage, async (sender, senderId, message, messageId, parcel) =>
        {
            if(MethodsByName.TryGetValue("Handle" + message, out var method))
            {
                await InitializeAsync();
                await Connection.InvokeAsync(Contract.Log, Me, Id, $"processing {message}");

                await Job.WithStep($"{message}", async state =>
                {
                    var parameterType = method.GetParameters()[0].ParameterType;  // TODO: change hardcoded zeroth element
                    var package = Deserialize(parcel, parameterType);
                    var res = method.Invoke(MessageHub, [package, state.State]);

                    if (res is not null && res.GetType().GetGenericTypeDefinition() == typeof(Task<object>))
                    {
                        var response = await (Task<object>)res;
                        await Connection.SendAsync(Contract.SendResponse, Me, Id, messageId, response);
                    }
                })
                .Start();
            }
        });

        Connection.On<string, string, Guid, object>(Contract.ReceiveResponse, async (sender, senderId, messageId, response) =>
        {
            var callback = MessageHub.GetCallback(messageId);

            if (callback is null)
            {
                await Connection.InvokeAsync(Contract.Log, Me, Id, $"Error: response arrived but no callback registered.");
                return;
            }

            callback(response);
            MessageHub.RemoveCallback(messageId);
        });
        
        Connection.Reconnecting += (sender) => Connection.InvokeAsync(Contract.Log, Me, Id, "Attempting to reconnect...");
        Connection.Reconnected += (sender) => Connection.InvokeAsync(Contract.Log, Me, Id, "Reconnected to the server");
        Connection.Closed += (sender) => Connection.InvokeAsync(Contract.Log, Me, Id, "Connection Closed");

        await Connection.StartAsync(cancellationToken);        
    }

    private object? Deserialize(string? parcel, Type type)
    {
        if (parcel is null) return parcel;
        return typeof(JsonSerializer)?.GetMethod(nameof(JsonSerializer.DeserializeAsync), [type])?.Invoke(null, [parcel]);
    }
}
