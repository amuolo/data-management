using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;

namespace Agency;

public class MessageHub<IContract> : Hub<IContract>
    where IContract : class
{
    public HubConnection Connection { get; }

    public string Me => GetType().Name;

    public string Id => Connection?.ConnectionId?? "";

    public bool IsConnected => Connection?.State == HubConnectionState.Connected;

    protected ConcurrentDictionary<Guid, Func<string, Task>> CallbacksById { get; } = new();

    protected ConcurrentDictionary<string, (Type? Type, Delegate Action)> OperationByPredicate { get; } = new();

    private MethodInfo[] Predicates { get; } = new[] { typeof(IContract) }.Concat(typeof(IContract).GetInterfaces())
                                                                          .SelectMany(i => i.GetMethods())
                                                                          .ToArray();

    public MessageHub()
    {
        Connection = new HubConnectionBuilder().WithUrl(Contract.Url).WithAutomaticReconnect().Build();
    }

    public void Dispose()
    {
        base.Dispose();
        Connection.StopAsync().Wait();
        Connection.DisposeAsync().AsTask().Wait();
    }

    protected bool GetMessage(Expression<Func<IContract, Delegate>> predicate, out string message)
    {
        // TODO: improve this mechanism with which name is retrieved from delegate in expression
        message = string.Empty;
        var msg = Predicates.FirstOrDefault(m => predicate.ToString().Contains(m.Name))?.Name;
        if (msg is null) return false;
        message = msg;
        return true;
    }

    protected bool IsAlive()
    {
        if (Connection is not null && !IsConnected)
        {
            // TODO: check whether to force reconnection at this point
            //Task.Run(async () => {
            //    await Connection.StartAsync();
            //    await LogAsync($"Hub disconnected.");
            //});
            return false;
        }

        return true;
    }

    /***********************
     *  Standard Messages  *
     ***********************/
    public async Task LogAsync(string msg) => await Connection.InvokeAsync(Contract.Log, Me, Id, msg);

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

        Task.Run(async () => await LogAsync(message));
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
            await LogAsync($"processing response {typeof(TResponse).Name}");
            try
            {
                var package = JsonSerializer.Deserialize<TResponse>(responseParcel);
                if (package is not null) 
                    callback(package);
                else
                    await LogAsync($"Error: null response after deserialization");
            }
            catch (Exception ex)
            {
                await LogAsync($"Error: Exception thrown: {ex.Message}");
            }
        });

        Task.Run(async () => await LogAsync(message));
        Task.Run(async () => await Connection.SendAsync(Contract.SendMessage, Me, Id, receiverId, message, messageId, parcel));
    }

    /*************************
     * Initialize Connection *
     * ***********************/
    public async Task InitializeConnectionAsync(CancellationToken cancellationToken)
        => await InitializeConnectionAsync(cancellationToken, ActionMessageReceived);

    public async Task InitializeConnectionAsync
        (CancellationToken cancellationToken, 
         Func<string, string, string, string, string?, Task> actionMessageReceived)
    {
        Connection.On<string, string, string, string, string?>(Contract.ReceiveMessage, 
            async (sender, senderId, message, messageId, parcel) =>
                await actionMessageReceived(sender, senderId, message, messageId, parcel));

        Connection.On<string, string, Guid, string>(Contract.ReceiveResponse,
            async (sender, senderId, messageId, response) =>
                await ActionResponseReceived(sender, senderId, messageId, response));

        Connection.Reconnecting += (sender) => LogAsync("Attempting to reconnect...");
        Connection.Reconnected += (sender) => LogAsync("Reconnected to the server");
        Connection.Closed += (sender) => LogAsync("Connection Closed");

        await Connection.StartAsync(cancellationToken);
    }

    internal async Task ActionMessageReceived(string sender, string senderId, string message, string messageId, string? parcel)
    {
        if (!OperationByPredicate.TryGetValue(message, out var operation)) return;

        if (operation.Type is null || !operation.Action.GetMethodInfo().GetParameters().Any())
        {
            await LogAsync($"processing {message}");
            operation.Action.DynamicInvoke();
        }
        else
        {
            if (parcel is null)
            {
                await LogAsync($"Warning: {message} received but {operation.Type.Name} expected.");
                return;
            }

            var package = JsonSerializer.Deserialize(parcel, operation.Type);

            if (package is null)
            {
                await LogAsync($"Warning: {message} received but {operation.Type.Name} failed deserialization.");
                return;
            }

            await LogAsync($"processing {message}");
            operation.Action.DynamicInvoke(package);
        }
    }

    internal async Task ActionResponseReceived(string sender, string senderId, Guid messageId, string response)
    {
        if (!CallbacksById.TryGetValue(messageId, out var callback))
        {
            await LogAsync($"Warning: response has arrived but left unhandled.");
            return;
        }

        if (response is not null) await callback(response);
        CallbacksById.Remove(messageId, out var value);
    }
}
