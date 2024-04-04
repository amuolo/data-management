using Microsoft.AspNetCore.SignalR;

namespace Enterprise.MessageHub;

public class ServerHub : Hub
{
    public Task Log(string sender, string senderId, string message)
    {
        return Clients.All.SendAsync(MessageType.ReceiveLog, sender, senderId, message);
    }

    public Task SendMessage(string sender, string senderId, string? receiverId, string message, string messageId, string? parcel)
    {
        if (receiverId is not null)
            return Clients.Client(receiverId).SendAsync(MessageType.ReceiveMessage, sender, senderId, message, messageId, parcel);
        else
            return Clients.All.SendAsync(MessageType.ReceiveMessage, sender, senderId, message, messageId, parcel);
    }

    public Task SendResponse(string sender, string senderId, string? receiverId, Guid messageId, string response)
    {
        if (receiverId is not null)
            return Clients.Client(receiverId).SendAsync(MessageType.ReceiveResponse, sender, senderId, messageId, response);
        else
            return Clients.All.SendAsync(MessageType.ReceiveResponse, sender, senderId, messageId, response);
    }

    public Task EstablishConnection(string sender, string senderId, string receiverId, Guid messageId)
    {
        return Clients.Client(receiverId).SendAsync(MessageType.ConnectionEstablished, sender, senderId, messageId);
    }
}
