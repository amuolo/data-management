﻿using Enterprise.MessageHub;
using Enterprise.Utils;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;

namespace Enterprise.Agency;

public class MessageHub<IContract> where IContract : class
{
    public HubConnection Connection { get; }

    public string Me => GetType().ExtractName();

    public string Id => Connection?.ConnectionId?? "";

    protected CancellationTokenSource TokenSource { get; } = new();

    protected bool IsServerAlive { get; set; }

    protected bool IsConnected => Connection is not null && Connection?.State == HubConnectionState.Connected;

    private event EventHandler NewMessageInOutbox;

    private SemaphoreSlim Semaphore { get; } = new(1, 1);

    private ConcurrentQueue<Parcel<IContract>> Outbox { get; } = new();

    private ConcurrentDictionary<Guid, Action<string>> CallbacksById { get; } = new();

    public ConcurrentDictionary<string, (Type? Type, Delegate Action)> OperationByPredicate { get; } = new();

    private MethodInfo[] Predicates { get; } = new[] { typeof(IContract) }.Concat(typeof(IContract).GetInterfaces())
                                                                           .SelectMany(i => i.GetMethods())
                                                                           .ToArray();

    public MessageHub()
    {
        Connection = new HubConnectionBuilder().WithUrl(Constants.Url).WithAutomaticReconnect().Build();
        NewMessageInOutbox += SendingMessage;
    }

    public void Dispose()
    {
        //base.Dispose();
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
            await Task.Delay(TimeSpans.ServerConnectionAttemptPeriod).ConfigureAwait(false);
    }

    private async Task ConnectToServer(CancellationToken token)
    {
        var id = Guid.NewGuid();
        var connected = false;
        var timerReconnection = new PeriodicTimer(TimeSpans.ServerConnectionAttemptPeriod);

        CallbacksById.TryAdd(id, _ => {
            connected = true;
            timerReconnection.Dispose();
        });

        do
        {
            await Connection.SendAsync(Constants.SendMessage, Me, Id, null, Constants.ConnectToServer, id, null).ConfigureAwait(false);
            await timerReconnection.WaitForNextTickAsync().ConfigureAwait(false);
            IsServerAlive = connected;
        }
        while (!token.IsCancellationRequested && !IsServerAlive);
    }

    private void SendingMessage(object? sender, EventArgs e)
    {
        if (Semaphore.CurrentCount == 0) return;

        Task.Run(async () =>
        {
            await Semaphore.WaitAsync(TokenSource.Token).ConfigureAwait(false);
            await WaitConnection().ConfigureAwait(false);
            await ConnectToServer(TokenSource.Token).ConfigureAwait(false);

            while (!Outbox.IsEmpty && !TokenSource.IsCancellationRequested)
            {
                var ok = Outbox.TryDequeue(out var parcel);

                if (!ok || parcel is null)
                {
                    LogPost("Issue with Outbox dequeuing");
                    return;
                }
                else if (parcel.Type == Constants.SendMessage)
                {
                    var receiverId = parcel.Address?.ToString();
                    var box = parcel.Package is not null ? JsonSerializer.Serialize(parcel.Package) : null;

                    LogPost(parcel.Message);
                    await Connection.SendAsync(Constants.SendMessage, Me, Id, receiverId, parcel.Message, parcel.Id, box).ConfigureAwait(false);
                }
                else if (parcel.Type == Constants.Log)
                {
                    await Connection.InvokeAsync(Constants.Log, Me, Id, parcel.Message).ConfigureAwait(false);
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
        var parcel = new Parcel<IContract>(null, null, msg) with { Type = Constants.Log };
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
                if (responseParcel is null)
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
        Connection.On(Constants.ReceiveMessage, actionMessageReceived);

        await FinalizeConnectionAsync(cancellationToken);
    }

    public async Task InitializeConnectionAsync
        (CancellationToken cancellationToken, Func<string, string, string, string, string?, Task> actionMessageReceived)
    {
        Connection.On<string, string, string, string, string?>(Constants.ReceiveMessage,
            async (sender, senderId, message, messageId, parcel) =>
                await actionMessageReceived(sender, senderId, message, messageId, parcel));

        await FinalizeConnectionAsync(cancellationToken);
    }

    private async Task FinalizeConnectionAsync(CancellationToken cancellationToken)
    {
        Connection.On<string, string, Guid, string>(Constants.ReceiveResponse, ActionResponseReceived);

        string getMsg(Exception? exc) => exc is null ? "" : "Exception: " + exc.Message;

        Connection.Reconnecting += (exc) => { LogPost($"Attempting to reconnect... {getMsg(exc)}"); return Task.CompletedTask; };
        Connection.Reconnected += (id) => { LogPost("Reconnected to the server"); return Task.CompletedTask; };
        Connection.Closed += (exc) => { LogPost($"Connection Closed! {getMsg(exc)}"); return Task.CompletedTask; };

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
