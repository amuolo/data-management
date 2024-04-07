using Microsoft.AspNetCore.SignalR;

namespace Enterprise.MessageHub;

public class ServerHub : Hub
{
    public Task Log(string sender, string senderId, string message)
    {
        return Clients.All.SendAsync(MessageTypes.ReceiveLog, sender, senderId, message);
    }

    public Task SendMessage(string sender, string senderId, string? receiverId, string message, string messageId, string? parcel)
    {
        if (receiverId is not null)
            return Clients.Client(receiverId).SendAsync(MessageTypes.ReceiveMessage, sender, senderId, message, messageId, parcel);
        else
            return Clients.All.SendAsync(MessageTypes.ReceiveMessage, sender, senderId, message, messageId, parcel);
    }

    public Task SendResponse(string sender, string senderId, string? receiverId, Guid messageId, string response)
    {
        if (receiverId is not null)
            return Clients.Client(receiverId).SendAsync(MessageTypes.ReceiveResponse, sender, senderId, messageId, response);
        else
            return Clients.All.SendAsync(MessageTypes.ReceiveResponse, sender, senderId, messageId, response);
    }

    public Task EstablishConnection(string sender, string senderId, string receiverId, Guid messageId)
    {
        return Clients.Client(receiverId).SendAsync(MessageTypes.ConnectionEstablished, sender, senderId, messageId);
    }
}
