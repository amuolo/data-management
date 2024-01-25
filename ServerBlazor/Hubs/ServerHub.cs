using Agency;

using Microsoft.AspNetCore.SignalR;

namespace ServerBlazor.Hubs;

public class ServerHub : Hub
{
    public Task Log(string sender, string senderId, string message)
    {
        return Clients.All.SendAsync(Contract.ReceiveLog, sender, senderId, message);
    }

    public Task SendMessage(string sender, string senderId, string? receiverId, string message, string messageId, string? parcel = null)
    {
        if(receiverId is not null)
            return Clients.Client(receiverId).SendAsync(Contract.ReceiveMessage, sender, senderId, message, messageId, parcel);
        else 
            return Clients.All.SendAsync(Contract.ReceiveMessage, sender, senderId, message, messageId, parcel);
    }

    public Task SendResponse(string sender, string senderId, string? receiverId, Guid messageId, object? response)
    {
        //if (receiverId is not null)
        //    return Clients.Client(receiverId).SendAsync(Contract.ReceiveResponse, sender, senderId, messageId, response);
        //else
            return Clients.All.SendAsync(Contract.ReceiveResponse, sender, senderId, messageId, response);
    }
}
