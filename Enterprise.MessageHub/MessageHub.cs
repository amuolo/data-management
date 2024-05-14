using Enterprise.Utils;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace Enterprise.MessageHub;

public class MessageHub<IContract> : IDisposable where IContract : class, IHubContract
{
    protected CancellationTokenSource Cancellation { get; } = new();

    public SmartStore<Parcel> Queue { get; } = new();

    public HubConnection Connection { get; protected set; }

    public string Id => Connection.ConnectionId?? "";

    public string Me { get; set; }

    public ConcurrentDictionary<string, Action<string>> CallbacksById { get; } = new();

    public ConcurrentDictionary<string, (Type? Type, Delegate Delegate)> OperationByPredicate { get; } = new();

    private MethodInfo[] Predicates { get; } = typeof(IContract).GetInterfaces().Concat([typeof(IContract)])
                                                                .SelectMany(i => i.GetMethods())
                                                                .ToArray();

    public static THub Create<THub> (string baseUrl) where THub : MessageHub<IContract>, new()
    {
        var r = new THub();
        if (baseUrl is null || !baseUrl.Any()) return r;
        var url = (baseUrl.LastOrDefault().Equals('/') ? baseUrl[..^1] : baseUrl) + Addresses.SignalR;
        r.Connection = new HubConnectionBuilder().WithUrl(url).WithAutomaticReconnect().Build();
        return r;
    }

    protected MessageHub() 
    {
        Me = GetType().ExtractName();
    }

    public MessageHub(string baseUrl)
    {
        Me = GetType().ExtractName();
        Connection = new HubConnectionBuilder().WithUrl(baseUrl + Addresses.SignalR).WithAutomaticReconnect().Build();
    }

    public virtual void Dispose()
    {
        Connection.Remove(PostingHub.ReceiveMessage);
        Connection.Remove(PostingHub.ReceiveResponse);
        Connection.Remove(PostingHub.ReceiveConnectRequest);        
        Connection.StopAsync();
        Cancellation.Cancel();
    }

    protected string GetMessage<TExpression>(TExpression predicate) where TExpression : Expression
    {
        // TODO: improve this mechanism
        var msg = Predicates.FirstOrDefault(m => predicate.ToString().Contains(m.Name + "("))?.Name;
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
        var parcel = new Parcel(default, default, default, msg) with { Type = nameof(PostingHub.Log) };
        Queue.Enqueue(parcel);
    }

    /*******************
       Post and forget 
     *******************/
    public void Post(string message) => Queue.Enqueue(new Parcel(default, default, default, message));

    public void Post(Expression<Func<IContract, Action>> predicate) => Post(default(IHubAddress), predicate);
    public void Post(Expression<Func<IContract, Func<Task>>> predicate) => Post(default(IHubAddress), predicate);

    public void Post<TAddress>(TAddress? address, Expression<Func<IContract, Action>> predicate) where TAddress : IHubAddress => Queue.Enqueue(new Parcel(address, default, default, GetMessage(predicate)));
    public void Post<TAddress>(TAddress? address, Expression<Func<IContract, Func<Task>>> predicate) where TAddress : IHubAddress => Queue.Enqueue(new Parcel(address, default, default, GetMessage(predicate)));

    public void Post<TSent>(Expression<Func<IContract, Action<TSent>>> predicate, TSent? package) => Post(default(IHubAddress), predicate, package);
    public void Post<TSent>(Expression<Func<IContract, Func<TSent, Task>>> predicate, TSent? package) => Post(default(IHubAddress), predicate, package);

    public void Post<TAddress, TSent>(TAddress? address, Expression<Func<IContract, Action<TSent>>> predicate, TSent? package) where TAddress : IHubAddress => Queue.Enqueue(new Parcel(address, default, package, GetMessage(predicate)));
    public void Post<TAddress, TSent>(TAddress? address, Expression<Func<IContract, Func<TSent, Task>>> predicate, TSent? package) where TAddress : IHubAddress => Queue.Enqueue(new Parcel(address, default, package, GetMessage(predicate)));

    /**********************
       Post with response 
     **********************/
    public void PostWithResponse<TResponse>(Expression<Func<IContract, Func<TResponse>>> predicate, Action<TResponse> callback) => PostWithResponse(default(IHubAddress), GetMessage(predicate), default(object), callback);
    public void PostWithResponse<TResponse>(Expression<Func<IContract, Func<Task<TResponse>>>> predicate, Action<TResponse> callback) => PostWithResponse(default(IHubAddress), GetMessage(predicate), default(object), callback);

    public void PostWithResponse<TAddress, TResponse>(TAddress? address, Expression<Func<IContract, Func<TResponse>>> predicate, Action<TResponse> callback) where TAddress : IHubAddress => PostWithResponse(address, GetMessage(predicate), default(object), callback);
    public void PostWithResponse<TAddress, TResponse>(TAddress? address, Expression<Func<IContract, Func<Task<TResponse>>>> predicate, Action<TResponse> callback) where TAddress : IHubAddress => PostWithResponse(address, GetMessage(predicate), default(object), callback);

    public void PostWithResponse<TSent, TResponse>(Expression<Func<IContract, Func<TSent, TResponse>>> predicate, TSent? package, Action<TResponse> callback) => PostWithResponse(default(IHubAddress), predicate, package, callback);
    public void PostWithResponse<TSent, TResponse>(Expression<Func<IContract, Func<TSent, Task<TResponse>>>> predicate, TSent? package, Action<TResponse> callback) => PostWithResponse(default(IHubAddress), predicate, package, callback);

    public void PostWithResponse<TAddress, TSent, TResponse>(TAddress? address, Expression<Func<IContract, Func<TSent, TResponse>>> predicate, TSent? package, Action<TResponse> callback) where TAddress : IHubAddress => PostWithResponse(address, GetMessage(predicate), package, callback);
    public void PostWithResponse<TAddress, TSent, TResponse>(TAddress? address, Expression<Func<IContract, Func<TSent, Task<TResponse>>>> predicate, TSent? package, Action<TResponse> callback) where TAddress : IHubAddress => PostWithResponse(address, GetMessage(predicate), package, callback);

    public void PostWithResponse<TAddress, TSent, TResponse>
        (TAddress? address, string message, TSent? package, Action<TResponse> callback) where TAddress : IHubAddress
    {
        var parcel = new Parcel(address, null, package, message);
 
        var ok = CallbacksById.TryAdd(parcel.Id, (string responseParcel) =>
        {
            LogPost($"processing response {message} {typeof(TResponse).Name}");
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
        });

        if (!ok)
            LogPost($"Callback registration failed for {message}.");

        Queue.Enqueue(parcel);
    }

    /**********************
            Actions 
     **********************/
    protected async Task StandardActionMessageReceivedAsync(string sender, string senderId, string message, string messageId, string? package)
    {
        if (!OperationByPredicate.TryGetValue(message, out var operation)) return;

        LogPost($"processing {message}");
        object? r;
       
        if (operation.Type is null)
        {
            r = operation.Delegate.DynamicInvoke();
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

            r = operation.Delegate.DynamicInvoke(item);
        }

        var returnType = operation.Delegate.Method.ReturnType;
        if (returnType != typeof(void) || returnType != typeof(Task))
        {
            if(r is null)
            {
                LogPost($"Warning: null result found for {message}");
                return;
            }
            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                await (Task)r;
                r = r.GetType().GetProperty("Result")?.GetValue(r);
            }
            Queue.Enqueue(new Parcel(sender, senderId, r, message)
                with { Type = nameof(PostingHub.SendResponse), Id = messageId });
        }
    }

    private void ActionResponseReceived(string sender, string senderId, string messageId, string response)
    {
        if (!CallbacksById.TryGetValue(messageId, out var callback))
        {
            LogPost($"warning: response {response} has arrived but left unhandled.");
            return;
        }

        try
        {
            callback(response);
        }
        catch (Exception ex)
        {
            LogPost($"Error: exception found while processing response: {ex.Message}");
        }
        CallbacksById.Remove(messageId, out var value);
    }

    /*************************
       Initialize Connection 
     *************************/
    public async Task InitializeConnectionAsync(CancellationToken token)
        => await InitializeConnectionAsync(token, StandardActionMessageReceivedAsync);

    public async Task InitializeConnectionAsync(CancellationToken token, Func<string, string, string, string, string?, Task> actionMessageReceivedAsync)
    {
        Connection.On<string, string, string, string, string?>(PostingHub.ReceiveMessage,
            async (sender, senderId, message, messageId, package) =>
                await actionMessageReceivedAsync(sender, senderId, message, messageId, package));

        await FinalizeConnectionAsync(token);
    }

    private async Task FinalizeConnectionAsync(CancellationToken token)
    {
        Connection.On(PostingHub.ReceiveConnectRequest,
            async (string sender, string senderId, string requestId, string target) =>
            {
                if (target == Me)
                    await Connection.SendAsync(nameof(PostingHub.ConnectionEstablished), Me, Id, senderId, requestId);
            });

        Connection.On<string, string, string, string>(PostingHub.ReceiveResponse, ActionResponseReceived);

        string getMsg(Exception? exc) => exc is null ? "" : "Exception: " + exc.Message;

        Connection.Reconnecting += (exc) => { LogPost($"Attempting to reconnect... {getMsg(exc)}"); return Task.CompletedTask; };
        Connection.Reconnected += (id) => { LogPost("Reconnected to the server"); return Task.CompletedTask; };
        Connection.Closed += (exc) => { LogPost($"Connection Closed! {getMsg(exc)}"); return Task.CompletedTask; };

        using (var timerReconnection = new PeriodicTimer(TimeSpans.ActorConnectionAttemptPeriod))
        {
            do
            {
                try
                {
                    await Connection.StartAsync(token);
                }
                catch (Exception e)
                {
                    LogPost(e.Message);
                }
                await timerReconnection.WaitForNextTickAsync().ConfigureAwait(false);
            }
            while (Connection.State != HubConnectionState.Connected && !token.IsCancellationRequested);
        }
    }
}
