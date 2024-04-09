﻿using Enterprise.MessageHub;
using Enterprise.Utils;
using Microsoft.AspNetCore.SignalR.Client;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;

namespace Enterprise.Agency;

public class MessageHub<IContract> where IContract : class
{
    protected CancellationTokenSource Cancellation { get; } = new();

    public SmartQueue<Parcel> Queue { get; }

    public HubConnection Connection { get; } = new HubConnectionBuilder().WithUrl(Addresses.Url).WithAutomaticReconnect().Build();

    public ConcurrentDictionary<Guid, Action<string>> CallbacksById { get; } = new();

    public ConcurrentDictionary<string, (Type? Type, Delegate Action)> OperationByPredicate { get; } = new();

    private MethodInfo[] Predicates { get; } = new[] { typeof(IContract) }.Concat(typeof(IContract).GetInterfaces())
                                                                          .SelectMany(i => i.GetMethods())
                                                                          .ToArray();

    public MessageHub()
    {
        Queue = new(GetType().ExtractName());
    }

    public void Dispose()
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
            // TODO: add logging
            return "";
        }
        return msg;
    }

    /***************
         Logging   
     ***************/
    public void LogPost(string msg)
    {
        var parcel = new Parcel(null, null, msg) with { Type = MessageTypes.Log };
        Queue.Enqueue(parcel);
    }

    /*******************
       Post and forget 
     *******************/
    public void Post(Expression<Func<IContract, Action>> predicate) => Post(default(object), predicate);
    public void Post(Expression<Func<IContract, Func<Task>>> predicate) => Post(default(object), predicate);

    public void Post<TAddress>(TAddress? address, Expression<Func<IContract, Action>> predicate) => Queue.Enqueue(new Parcel(address, default, GetMessage(predicate)));
    public void Post<TAddress>(TAddress? address, Expression<Func<IContract, Func<Task>>> predicate) => Queue.Enqueue(new Parcel(address, default, GetMessage(predicate)));

    public void Post<TSent>(Expression<Func<IContract, Action<TSent>>> predicate, TSent? package) => Post(default(object), predicate, package);
    public void Post<TSent>(Expression<Func<IContract, Func<TSent, Task>>> predicate, TSent? package) => Post(default(object), predicate, package);

    public void Post<TAddress, TSent>(TAddress? address, Expression<Func<IContract, Action<TSent>>> predicate, TSent? package) => Queue.Enqueue(new Parcel(address, package, GetMessage(predicate)));
    public void Post<TAddress, TSent>(TAddress? address, Expression<Func<IContract, Func<TSent, Task>>> predicate, TSent? package) => Queue.Enqueue(new Parcel(address, package, GetMessage(predicate)));

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
        var parcel = new Parcel(address, package, message);
 
        CallbacksById.TryAdd(parcel.Id, (string responseParcel) =>
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

        Queue.Enqueue(parcel);
    }

    /**********************
            Actions 
     **********************/

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

    /*************************
       Initialize Connection 
     *************************/
    public async Task InitializeConnectionAsync(CancellationToken cancellationToken)
        => await InitializeConnectionAsync(cancellationToken, ActionMessageReceived);

    public async Task InitializeConnectionAsync
        (CancellationToken cancellationToken, Action<string, string, string, string, string?> actionMessageReceived)
    {
        Connection.On(MessageTypes.ReceiveMessage, actionMessageReceived);

        await FinalizeConnectionAsync(cancellationToken);
    }

    public async Task InitializeConnectionAsync
        (CancellationToken cancellationToken, Func<string, string, string, string, string?, Task> actionMessageReceived)
    {
        Connection.On<string, string, string, string, string?>(MessageTypes.ReceiveMessage,
            async (sender, senderId, message, messageId, parcel) =>
                await actionMessageReceived(sender, senderId, message, messageId, parcel));

        await FinalizeConnectionAsync(cancellationToken);
    }

    private async Task FinalizeConnectionAsync(CancellationToken cancellationToken)
    {
        Connection.On<string, string, Guid, string>(MessageTypes.ReceiveResponse, ActionResponseReceived);

        string getMsg(Exception? exc) => exc is null ? "" : "Exception: " + exc.Message;

        Connection.Reconnecting += (exc) => { LogPost($"Attempting to reconnect... {getMsg(exc)}"); return Task.CompletedTask; };
        Connection.Reconnected += (id) => { LogPost("Reconnected to the server"); return Task.CompletedTask; };
        Connection.Closed += (exc) => { LogPost($"Connection Closed! {getMsg(exc)}"); return Task.CompletedTask; };

        await Connection.StartAsync(cancellationToken);
    }
}
