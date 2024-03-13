using Microsoft.AspNetCore.SignalR;

namespace Enterprise.MessageHub;

public class ServerHub : Hub
{
    public Task Log(string sender, string senderId, string message)
    {
        return Clients.All.SendAsync(Constants.ReceiveLog, sender, senderId, message);
    }

    public Task SendMessage(string sender, string senderId, string? receiverId, string message, string messageId, string? parcel)
    {
        if (receiverId is not null)
            return Clients.Client(receiverId).SendAsync(Constants.ReceiveMessage, sender, senderId, message, messageId, parcel);
        else
            return Clients.All.SendAsync(Constants.ReceiveMessage, sender, senderId, message, messageId, parcel);
    }

    public Task SendResponse(string sender, string senderId, string? receiverId, Guid messageId, string response)
    {
        if (receiverId is not null)
            return Clients.Client(receiverId).SendAsync(Constants.ReceiveResponse, sender, senderId, messageId, response);
        else
            return Clients.All.SendAsync(Constants.ReceiveResponse, sender, senderId, messageId, response);
    }
}
