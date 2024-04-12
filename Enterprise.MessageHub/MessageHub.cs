using Enterprise.MessageHub;
using Enterprise.Utils;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace Enterprise.Agency;

public class MessageHub<IContract> where IContract : class, IHubContract
{
    protected bool IsLogger { get; set; } = false;

    protected CancellationTokenSource Cancellation { get; } = new();

    public SmartStore<Parcel> Queue { get; }

    public HubConnection Connection { get; protected set; } = new HubConnectionBuilder().WithUrl(Addresses.Url).WithAutomaticReconnect().Build();

    public string Id => Connection.ConnectionId?? throw new ArgumentNullException(nameof(HubConnection));

    public string Me => IsLogger ? Addresses.Logger : GetType().ExtractName();

    public ConcurrentDictionary<string, Action<string>> CallbacksById { get; } = new();

    public ConcurrentDictionary<string, (Type? Type, Delegate Action)> OperationByPredicate { get; } = new();

    private MethodInfo[] Predicates { get; } = new[] { typeof(IContract) }.Concat(typeof(IContract).GetInterfaces())
                                                                          .SelectMany(i => i.GetMethods())
                                                                          .ToArray();

    public MessageHub()
    {
        Queue = new(Me);
        ActionMessageReceived = StandardActionMessageReceivedAsync;
    }

    public virtual void Dispose()
    {
        Cancellation.Cancel();
    }
    
    protected string GetMessage<TExpression>(TExpression predicate) where TExpression : Expression
    {
        // TODO: improve this mechanism with which name is retrieved from delegate in expression
        var msg = Predicates.FirstOrDefault(m => predicate.ToString().Contains(m.Name))?.Name;
        if (msg is null)
        {
            LogPost($"MessageHub delegate resolution failed for: {predicate}.");
            return "";
        }
        return msg;
    }

    /***************
         Logging   
     ***************/
    public void LogPost(string msg)
    {
        // TODO: add logging
        var parcel = new Parcel(default, default, default, msg) with { Type = nameof(ServerHub.Log) };
        Queue.Enqueue(parcel);
    }

    /*******************
       Post and forget 
     *******************/
    public void Post(string message) => Queue.Enqueue(new Parcel(default, default, default, message));

    public void Post(Expression<Func<IContract, Action>> predicate) => Post(default(object), predicate);
    public void Post(Expression<Func<IContract, Func<Task>>> predicate) => Post(default(object), predicate);

    public void Post<TAddress>(TAddress? address, Expression<Func<IContract, Action>> predicate) => Queue.Enqueue(new Parcel(address, default, default, GetMessage(predicate)));
    public void Post<TAddress>(TAddress? address, Expression<Func<IContract, Func<Task>>> predicate) => Queue.Enqueue(new Parcel(address, default, default, GetMessage(predicate)));

    public void Post<TSent>(Expression<Func<IContract, Action<TSent>>> predicate, TSent? package) => Post(default(object), predicate, package);
    public void Post<TSent>(Expression<Func<IContract, Func<TSent, Task>>> predicate, TSent? package) => Post(default(object), predicate, package);

    public void Post<TAddress, TSent>(TAddress? address, Expression<Func<IContract, Action<TSent>>> predicate, TSent? package) => Queue.Enqueue(new Parcel(address, default, package, GetMessage(predicate)));
    public void Post<TAddress, TSent>(TAddress? address, Expression<Func<IContract, Func<TSent, Task>>> predicate, TSent? package) => Queue.Enqueue(new Parcel(address, default, package, GetMessage(predicate)));

    /**********************
       Post with response 
     **********************/
    public void PostWithResponse<TResponse>(Expression<Func<IContract, Func<TResponse>>> predicate, Action<TResponse> callback) => PostWithResponse(default(object), GetMessage(predicate), default(object), callback);
    public void PostWithResponse<TResponse>(Expression<Func<IContract, Func<Task<TResponse>>>> predicate, Action<TResponse> callback) => PostWithResponse(default(object), GetMessage(predicate), default(object), callback);

    public void PostWithResponse<TAddress, TResponse>(TAddress? address, Expression<Func<IContract, Func<TResponse>>> predicate, Action<TResponse> callback) => PostWithResponse(address, GetMessage(predicate), default(object), callback);
    public void PostWithResponse<TAddress, TResponse>(TAddress? address, Expression<Func<IContract, Func<Task<TResponse>>>> predicate, Action<TResponse> callback) => PostWithResponse(address, GetMessage(predicate), default(object), callback);

    public void PostWithResponse<TSent, TResponse>(Expression<Func<IContract, Func<TSent, TResponse>>> predicate, TSent? package, Action<TResponse> callback) => PostWithResponse(default(object), predicate, package, callback);
    public void PostWithResponse<TSent, TResponse>(Expression<Func<IContract, Func<TSent, Task<TResponse>>>> predicate, TSent? package, Action<TResponse> callback) => PostWithResponse(default(object), predicate, package, callback);

    public void PostWithResponse<TAddress, TSent, TResponse>(TAddress? address, Expression<Func<IContract, Func<TSent, TResponse>>> predicate, TSent? package, Action<TResponse> callback) => PostWithResponse(address, GetMessage(predicate), package, callback);
    public void PostWithResponse<TAddress, TSent, TResponse>(TAddress? address, Expression<Func<IContract, Func<TSent, Task<TResponse>>>> predicate, TSent? package, Action<TResponse> callback) => PostWithResponse(address, GetMessage(predicate), package, callback);

    public void PostWithResponse<TAddress, TSent, TResponse>
        (TAddress? address, string message, TSent? package, Action<TResponse> callback)
    {
        var parcel = new Parcel(address, null, package, message);
 
        var ok = CallbacksById.TryAdd(parcel.Id, (string responseParcel) =>
        {
            LogPost($"processing response {message} {typeof(TResponse).Name}");
            try
            {
                if (responseParcel is null)
                {
                    callback(default);
                }
                else
                {
                    var package = JsonConvert.DeserializeObject<TResponse>(responseParcel);
                    if (package is not null)
                        callback(package);
                    else
                        LogPost($"Error: response deserialization failed");
                }
            }
            catch (Exception ex)
            {
                LogPost($"Error: Exception thrown: {ex.Message}");
            }
        });

        if (!ok)
            LogPost($"Callback registration failed for {message}.");

        Queue.Enqueue(parcel);
    }

    /**********************
            Actions 
     **********************/
    public Action<string, string, string, string, string?> ActionMessageReceived { get; set; }

    protected void StandardActionMessageReceivedAsync(string sender, string senderId, string message, string messageId, string? package)
    {
        if (!OperationByPredicate.TryGetValue(message, out var operation)) return;

        if (operation.Type is null || !operation.Action.GetMethodInfo().GetParameters().Any())
        {
            LogPost($"processing {message}");
            operation.Action.DynamicInvoke();
        }
        else
        {
            if (package is null)
            {
                LogPost($"Warning: {message} received but {operation.Type.Name} expected.");
                return;
            }

            var item = JsonConvert.DeserializeObject(package, operation.Type);

            if (item is null)
            {
                LogPost($"Warning: {message} received but {operation.Type.Name} failed deserialization.");
                return;
            }

            LogPost($"processing {message}");
            operation.Action.DynamicInvoke(item);
        }
    }

    private void ActionResponseReceived(string sender, string senderId, string messageId, string response)
    {
        if (!CallbacksById.TryGetValue(messageId, out var callback))
        {
            LogPost($"warning: response {response} has arrived but left unhandled.");
            return;
        }

        callback(response);
        CallbacksById.Remove(messageId, out var value);
    }

    /*************************
       Initialize Connection 
     *************************/
    public async Task InitializeConnectionAsync(CancellationToken token)
        => await InitializeConnectionAsync(token, ActionMessageReceived);

    public async Task InitializeConnectionAsync(CancellationToken token, Action<string, string, string, string, string?> actionMessageReceived)
    {
        Connection.On(nameof(IHubContract.ReceiveMessage), actionMessageReceived);

        await FinalizeConnectionAsync(token);
    }

    public async Task InitializeConnectionAsync(CancellationToken token, Func<string, string, string, string, string?, Task> actionMessageReceived)
    {
        Connection.On<string, string, string, string, string?>(nameof(IHubContract.ReceiveMessage),
            async (sender, senderId, message, messageId, package) =>
                await actionMessageReceived(sender, senderId, message, messageId, package));

        await FinalizeConnectionAsync(token);
    }

    private async Task FinalizeConnectionAsync(CancellationToken token)
    {
        Connection.On(nameof(IHubContract.ConnectRequest),
            async (string sender, string senderId, string requestId, string target) =>
            {
                if (target == Me)
                    await Connection.SendAsync(nameof(IHubContract.ConnectionEstablished), Id, senderId, requestId);
            });

        Connection.On<string, string, string, string>(nameof(IHubContract.ReceiveResponse), ActionResponseReceived);

        string getMsg(Exception? exc) => exc is null ? "" : "Exception: " + exc.Message;

        Connection.Reconnecting += (exc) => { LogPost($"Attempting to reconnect... {getMsg(exc)}"); return Task.CompletedTask; };
        Connection.Reconnected += (id) => { LogPost("Reconnected to the server"); return Task.CompletedTask; };
        Connection.Closed += (exc) => { LogPost($"Connection Closed! {getMsg(exc)}"); return Task.CompletedTask; };

        await Connection.StartAsync(token);
    }
}
