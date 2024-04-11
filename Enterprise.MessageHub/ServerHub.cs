using Microsoft.AspNetCore.SignalR;

namespace Enterprise.MessageHub;

public class ServerHub : Hub
{
    public Task Log(string sender, string senderId, string message)
    {
        return Clients.All.SendAsync(nameof(IHubContract.ReceiveLog), sender, senderId, message);
    }

    public Task SendMessage(string sender, string senderId, string? receiverId, string message, string messageId, string? package)
    {
        if (receiverId is not null)
            return Clients.Client(receiverId).SendAsync(MessageTypes.ReceiveMessage, sender, senderId, message, messageId, package);
        else
            return Clients.All.SendAsync(MessageTypes.ReceiveMessage, sender, senderId, message, messageId, package);
    }

    public Task SendResponse(string sender, string senderId, string receiverId, string messageId, string response)
    {
        return Clients.Client(receiverId).SendAsync(MessageTypes.ReceiveResponse, sender, senderId, messageId, response);
    }

    public Task ConnectRequest(string sender, string senderId, string requestId, string target)
    {
        return Clients.All.SendAsync(nameof(IHubContract.ConnectRequest), sender, senderId, requestId, target);
    }

    public Task ConnectionEstablished(string senderId, string receiverId, string requestId)
    {
        return Clients.Client(receiverId).SendAsync(nameof(IHubContract.ConnectionEstablished) + requestId, senderId, requestId);
    }
}
