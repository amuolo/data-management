using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using System.Linq.Expressions;

namespace Agency;

public class MessageHub<IContract> : Hub<IContract>
    where IContract : class
{
    private HubConnection Connection { get; }

    bool IsConnected => Connection?.State == HubConnectionState.Connected;

    public MessageHub()
    {
        Connection = new HubConnectionBuilder().WithUrl(Contract.Url).WithAutomaticReconnect().Build();
        Connection.StartAsync();
    }

    public void Post(Expression<Func<IContract, Delegate>> expression, object? package = null)
    {
        Task.Run(async () =>
        {
            var methods = typeof(IContract).GetInterfaces().SelectMany(i => i.GetMethods()).ToArray();
            var method = methods.FirstOrDefault(m => expression.ToString().Contains(m.Name));
            if (method is not null && IsConnected)
                await Connection.SendAsync("SendMessage", GetType().Name, method.Name, package);
        });
    }
}
