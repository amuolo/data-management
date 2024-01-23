using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using System.Linq.Expressions;

namespace Agency;

public class MessageHub<IContract> : Hub<IContract>
    where IContract : class
{
    public Guid Id { get; } = Guid.NewGuid();

    private HubConnection Connection { get; }

    bool IsConnected => Connection?.State == HubConnectionState.Connected;

    public MessageHub()
    {
        Connection = new HubConnectionBuilder().WithUrl(Contract.Url).WithAutomaticReconnect().Build();
        Connection.StartAsync();
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
        Task.Run(async () =>
        {
            var methods = new[]{typeof(IContract)}.Concat(typeof(IContract).GetInterfaces())
                                                  .SelectMany(i => i.GetMethods())
                                                  .ToArray();

            // TODO: improve this mechanism with which name is retrieved from delegate in expression
            var method = methods.FirstOrDefault(m => predicate.ToString().Contains(m.Name));
            if (method is not null && IsConnected)
                await Connection.SendAsync(Contract.SendMessage, GetType().Name, Id, method.Name, package);
        });
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
        // TODO: finish this
        Post(address, predicate, package);
    }
}
