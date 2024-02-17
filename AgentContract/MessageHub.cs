using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Threading;

namespace Agency;

public class MessageHub<IContract> : Hub<IContract>
    where IContract : class
{
    public HubConnection Connection { get; }

    public string Me => GetType().Name;

    public string Id => Connection?.ConnectionId?? "";

    protected CancellationTokenSource TokenSource { get; } = new();

    protected bool IsServerAlive { get; set; }

    protected bool IsConnected => Connection is not null && Connection?.State == HubConnectionState.Connected;

    private event EventHandler NewMessageInOutbox;

    private SemaphoreSlim Semaphore { get; } = new(1, 1);

    private ConcurrentQueue<Parcel<IContract>> Outbox { get; } = new();

    private ConcurrentDictionary<Guid, Action<string>> CallbacksById { get; } = new();

    internal ConcurrentDictionary<string, (Type? Type, Delegate Action)> OperationByPredicate { get; } = new();

    private MethodInfo[] Predicates { get; } = new[] { typeof(IContract) }.Concat(typeof(IContract).GetInterfaces())
                                                                           .SelectMany(i => i.GetMethods())
                                                                           .ToArray();

    public MessageHub()
    {
        Connection = new HubConnectionBuilder().WithUrl(Consts.Url).WithAutomaticReconnect().Build();
        NewMessageInOutbox += SendingMessage;
    }

    public void Dispose()
    {
        base.Dispose();
        TokenSource.Cancel();
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

    private async Task WaitConnection()
    {
        while (!IsConnected)
            await Task.Delay(Consts.ServerConnectionAttemptPeriod);
    }

    private async Task ConnectToServer(CancellationToken token)
    {
        var id = Guid.NewGuid();
        var connected = false;
        var timerReconnection = new PeriodicTimer(Consts.ServerConnectionAttemptPeriod);

        CallbacksById.TryAdd(id, _ => {
            connected = true;
            timerReconnection.Dispose();
        });

        do
        {
            await Connection.SendAsync(Consts.SendMessage, Me, Id, null, Consts.ConnectToServer, id, null);
            await timerReconnection.WaitForNextTickAsync();
            IsServerAlive = connected;
        }
        while (!token.IsCancellationRequested && !IsServerAlive);
    }

    private void SendingMessage(object? sender, EventArgs e)
    {
        if (Semaphore.CurrentCount == 0) return;

        Task.Run(async () =>
        {
            await Semaphore.WaitAsync(TokenSource.Token);
            await WaitConnection();
            await ConnectToServer(TokenSource.Token);

            while (!Outbox.IsEmpty && !TokenSource.IsCancellationRequested)
            {
                var ok = Outbox.TryDequeue(out var parcel);

                if (!ok || parcel is null)
                {
                    LogPost("Issue with Outbox dequeuing");
                    return;
                }
                else if (parcel.Type == Consts.SendMessage)
                {
                    var receiverId = parcel.Address?.ToString();
                    var box = parcel.Package is not null ? JsonSerializer.Serialize(parcel.Package) : null;

                    LogPost(parcel.Message);
                    await Connection.SendAsync(Consts.SendMessage, Me, Id, receiverId, parcel.Message, parcel.Id, box);
                }
                else if(parcel.Type == Consts.Log)
                {
                    await Connection.InvokeAsync(Consts.Log, Me, Id, parcel.Message);
                }
            }

            Semaphore.Release();
        });
    }

    /***************
     *   Logging   *
     ***************/
    public void LogPost(string msg)
    {
        var parcel = new Parcel<IContract>(null, null, msg) with { Type = Consts.Log };
        Outbox.Enqueue(parcel);
        NewMessageInOutbox.Invoke(this, new EventArgs());
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
        if (!GetMessage(predicate, out var message)) return;

        Outbox.Enqueue(new Parcel<IContract>(address, package, message));
        NewMessageInOutbox.Invoke(this, new EventArgs());
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
        if (!GetMessage(predicate, out var message)) return;

        PostWithResponse(address, message, package, callback);
    }

    public void PostWithResponse<TAddress, TSent, TResponse>
        (TAddress? address, string message, TSent? package, Action<TResponse> callback)
    {
        var parcel = new Parcel<IContract>(address, package, message);
        Outbox.Enqueue(parcel);

        CallbacksById.TryAdd(parcel.Id, (string responseParcel) =>
        {
            LogPost($"processing response {typeof(TResponse).Name}");
            try
            {
                if(responseParcel is null)
                {
                    callback(default);
                }
                else
                {
                    var package = JsonSerializer.Deserialize<TResponse>(responseParcel);
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

        NewMessageInOutbox.Invoke(this, new EventArgs());
    }

    /*************************
     * Initialize Connection *
     * ***********************/
    public async Task InitializeConnectionAsync(CancellationToken cancellationToken)
        => await InitializeConnectionAsync(cancellationToken, ActionMessageReceived);

    public async Task InitializeConnectionAsync
        (CancellationToken cancellationToken, Action<string, string, string, string, string?> actionMessageReceived)
    {
        Connection.On(Consts.ReceiveMessage, actionMessageReceived);

        await FinalizeConnectionAsync(cancellationToken);
    }

    public async Task InitializeConnectionAsync
        (CancellationToken cancellationToken, Func<string, string, string, string, string?, Task> actionMessageReceived)
    {
        Connection.On<string, string, string, string, string?>(Consts.ReceiveMessage,
            async (sender, senderId, message, messageId, parcel) =>
                await actionMessageReceived(sender, senderId, message, messageId, parcel));

        await FinalizeConnectionAsync(cancellationToken);
    }

    private async Task FinalizeConnectionAsync(CancellationToken cancellationToken)
    {
        Connection.On<string, string, Guid, string>(Consts.ReceiveResponse, ActionResponseReceived);

        Connection.Reconnecting += (sender) => Task.Run(() => LogPost("Attempting to reconnect..."));
        Connection.Reconnected += (sender) => Task.Run(() => LogPost("Reconnected to the server"));
        Connection.Closed += (sender) => Task.Run(() => LogPost("Connection Closed"));

        await Connection.StartAsync(cancellationToken);
    }

    private void ActionMessageReceived(string sender, string senderId, string message, string messageId, string? parcel)
    {
        if (!OperationByPredicate.TryGetValue(message, out var operation)) return;

        if (operation.Type is null || !operation.Action.GetMethodInfo().GetParameters().Any())
        {
            LogPost($"processing {message}");
            operation.Action.DynamicInvoke();
        }
        else
        {
            if (parcel is null)
            {
                LogPost($"Warning: {message} received but {operation.Type.Name} expected.");
                return;
            }

            var package = JsonSerializer.Deserialize(parcel, operation.Type);

            if (package is null)
            {
                LogPost($"Warning: {message} received but {operation.Type.Name} failed deserialization.");
                return;
            }

            LogPost($"processing {message}");
            operation.Action.DynamicInvoke(package);
        }
    }

    internal void ActionResponseReceived(string sender, string senderId, Guid messageId, string response)
    {
        if (!CallbacksById.TryGetValue(messageId, out var callback))
        {
            LogPost($"Warning: response has arrived but left unhandled.");
            return;
        }

        callback(response);
        CallbacksById.Remove(messageId, out var value);
    }
}
