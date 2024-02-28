using Enterprise.Agency;
using Microsoft.AspNetCore.SignalR;

namespace Service.Dispatcher;

public class ServerHub : Hub
{
    public Task Log(string sender, string senderId, string message)
    {
        return Clients.All.SendAsync(Consts.ReceiveLog, sender, senderId, message);
    }

    public Task SendMessage(string sender, string senderId, string? receiverId, string message, string messageId, string? parcel)
    {
        if(receiverId is not null)
            return Clients.Client(receiverId).SendAsync(Consts.ReceiveMessage, sender, senderId, message, messageId, parcel);
        else 
            return Clients.All.SendAsync(Consts.ReceiveMessage, sender, senderId, message, messageId, parcel);
    }

    public Task SendResponse(string sender, string senderId, string? receiverId, Guid messageId, string response)
    {
        if (receiverId is not null)
            return Clients.Client(receiverId).SendAsync(Consts.ReceiveResponse, sender, senderId, messageId, response);
        else
            return Clients.All.SendAsync(Consts.ReceiveResponse, sender, senderId, messageId, response);
    }
}
