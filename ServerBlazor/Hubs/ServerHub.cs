using Agency;

using Microsoft.AspNetCore.SignalR;

namespace ServerBlazor.Hubs;

public class ServerHub : Hub
{
    public Task SendMessage(string sender, string? senderId, string? receiverId, string message, object? package = null)
    {
        if(receiverId is not null)
            return Clients.Client(receiverId).SendAsync(Contract.ReceiveMessage, sender, senderId, message, package);
        else 
            return Clients.All.SendAsync(Contract.ReceiveMessage, sender, senderId, message, package);
    }

    public Task Log(string sender, string? senderId, string message)
    {
        return Clients.All.SendAsync(Contract.ReceiveLog, sender, senderId, message);
    }

    public Task SendResponse(string sender, string? senderId, string receiverId, string message, object? package = null)
    {
        if(receiverId is not null)
            return Clients.Client(receiverId).SendAsync(Contract.ReceiveResponse, sender, senderId, message, package);
        else
            return Clients.All.SendAsync(Contract.ReceiveResponse, sender, senderId, message, package);
    }
}
