using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using System.Linq.Expressions;
using System.Reflection;

namespace Agency;

public class MessageHub<IContract> : Hub<IContract>
    where IContract : class
{
    public Guid Id { get; } = Guid.NewGuid();

    public HubConnection Connection { get; }

    public bool IsConnected => Connection?.State == HubConnectionState.Connected;

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
            Task.Run(async () => await Connection.SendAsync(Contract.Log, GetType().Name, Id, $"Hub disconnected."));
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
        // TODO: post to a specific address if not null
        if (!GetMessage(predicate, out var message)) return;
        if (!IsAlive()) return;

        Task.Run(async () => await Connection.SendAsync(Contract.Log, GetType().Name, Id, message));
        Task.Run(async () => await Connection.SendAsync(Contract.SendMessage, GetType().Name, Id, message, package));
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
        // TODO: post to a specific address if not null
        if (!GetMessage(predicate, out var message)) return;
        if (!IsAlive()) return;

        // TODO: finish this
        Connection.On<string, Guid, string, object?>(Contract.ReceiveResponse,
            async (sender, senderId, message, package) =>
            {

            });

        Connection.Remove(message);

        Post(address, predicate, package);
    }
}
