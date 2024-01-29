using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agency;

public class MessageHub<IContract> : Hub<IContract>
    where IContract : class
{
    public HubConnection Connection { get; }

    public string Me => GetType().Name;

    public string Id => Connection?.ConnectionId?? "";

    public bool IsConnected => Connection?.State == HubConnectionState.Connected;

    protected ConcurrentDictionary<Guid, Func<string, Task>> CallbacksById { get; } = new();

    private MethodInfo[] Predicates { get; } = new[] { typeof(IContract) }.Concat(typeof(IContract).GetInterfaces())
                                                                          .SelectMany(i => i.GetMethods())
                                                                          .ToArray();

    public MessageHub()
    {
        Connection = new HubConnectionBuilder().WithUrl(Contract.Url).WithAutomaticReconnect().Build();
    }

    private bool GetMessage(Expression<Func<IContract, Delegate>> predicate, out string message)
    {
        // TODO: improve this mechanism with which name is retrieved from delegate in expression
        message = string.Empty;
        var msg = Predicates.FirstOrDefault(m => predicate.ToString().Contains(m.Name))?.Name;
        if (msg is null) return false;
        message = msg;
        return true;
    }

    private bool IsAlive()
    {
        if (Connection is not null && !IsConnected)
        {
            Task.Run(async () => await Connection.SendAsync(Contract.Log, Me, Id, $"Hub disconnected."));
            return false;
        }

        return true;
    }

    /*******************
     * Post and forget *
     * *****************/
    public void Post(Expression<Func<IContract, Delegate>> predicate)
        => Post(default(object), predicate, default(object));

    public void Post<TAddress>(TAddress? address, Expression<Func<IContract, Delegate>> predicate)
        => Post(address, predicate, default(object));

    public void Post<TSent>(Expression<Func<IContract, Delegate>> predicate, TSent? package)
        => Post(default(object), predicate, package);

    public void Post<TAddress, TSent>(TAddress? address, Expression<Func<IContract, Delegate>> predicate, TSent? package)
    {
        if (!GetMessage(predicate, out var message) || !IsAlive()) return;

        // TODO: find a way to use the direct address provided in the parameters to enable point-to-point communications
        var receiverId = address?.ToString();
        var messageId = Guid.NewGuid();
        var parcel = package is not null ? JsonSerializer.Serialize(package) : null;

        Task.Run(async () => await Connection.SendAsync(Contract.Log, Me, Id, message));
        Task.Run(async () => await Connection.SendAsync(Contract.SendMessage, Me, Id, receiverId, message, messageId, parcel));
    }

    /**********************
     * Post with response *
     * ********************/
    public void PostWithResponse<TResponse>(Expression<Func<IContract, Delegate>> predicate, Action<TResponse> callback)
        => PostWithResponse(default(object), predicate, default(object), callback);

    public void PostWithResponse<TAddress, TResponse>(TAddress? address, Expression<Func<IContract, Delegate>> predicate, Action<TResponse> callback)
        => PostWithResponse(address, predicate, default(object), callback);

    public void PostWithResponse<TSent, TResponse>(Expression<Func<IContract, Delegate>> predicate, TSent? package, Action<TResponse> callback)
        => PostWithResponse(default(object), predicate, package, callback);

    public void PostWithResponse<TAddress, TSent, TResponse>
        (TAddress? address, Expression<Func<IContract, Delegate>> predicate, TSent? package, Action<TResponse> callback)
    {
        if (!GetMessage(predicate, out var message) || !IsAlive()) return;

        // TODO: find a way to use the direct address provided in the parameters to enable point-to-point communications
        var receiverId = address?.ToString();
        var messageId = Guid.NewGuid();
        var parcel = package is not null ? JsonSerializer.Serialize(package) : null;

        CallbacksById.TryAdd(messageId, async (string responseParcel) =>
        {
            await Connection.InvokeAsync(Contract.Log, Me, Id, $"processing response {typeof(TResponse).Name}");
            try
            {
                var package = JsonSerializer.Deserialize<TResponse>(responseParcel);
                if (package is not null) 
                    callback(package);
                else
                    await Connection.SendAsync(Contract.Log, Me, Id, $"Error: null response after deserialization");
            }
            catch (Exception ex)
            {
                await Connection.SendAsync(Contract.Log, Me, Id, $"Error: Exception thrown: {ex.Message}");
            }
        });

        Task.Run(async () => await Connection.SendAsync(Contract.Log, Me, Id, message));
        Task.Run(async () => await Connection.SendAsync(Contract.SendMessage, Me, Id, receiverId, message, messageId, parcel));
    }

    /*************************
     * Initialize Connection *
     * ***********************/
    public async Task InitializeConnectionAsync(CancellationToken cancellationToken)
    {
        Connection.On<string, string, Guid, string>(Contract.ReceiveResponse, 
            async (sender, senderId, messageId, response) =>
                await ActionResponseReceived(sender, senderId, messageId, response));

        Connection.Reconnecting += (sender) => Connection.InvokeAsync(Contract.Log, Me, Id, "Attempting to reconnect...");
        Connection.Reconnected += (sender) => Connection.InvokeAsync(Contract.Log, Me, Id, "Reconnected to the server");
        Connection.Closed += (sender) => Connection.InvokeAsync(Contract.Log, Me, Id, "Connection Closed");

        await Connection.StartAsync(cancellationToken);
    }

    public async Task InitializeConnectionAsync
        (CancellationToken cancellationToken, Func<string, string, string, string, string?, Task> actionMessageReceived)
    {
        Connection.On<string, string, string, string, string?>(Contract.ReceiveMessage, 
            async (sender, senderId, message, messageId, parcel) =>
                await actionMessageReceived(sender, senderId, message, messageId, parcel));

        await InitializeConnectionAsync(cancellationToken);
    }

    async Task ActionResponseReceived(string sender, string senderId, Guid messageId, string response)
    {
        CallbacksById.TryGetValue(messageId, out var callback);

        if (callback is null)
        {
            await Connection.InvokeAsync(Contract.Log, Me, Id, $"Warning: response has arrived but left unhandled.");
            return;
        }

        if (response is not null) await callback(response);
        CallbacksById.Remove(messageId, out var value);
    }
}
