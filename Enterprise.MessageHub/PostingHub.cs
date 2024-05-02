using Microsoft.AspNetCore.SignalR;

namespace Enterprise.MessageHub;

public class PostingHub : Hub
{
    public const string ReceiveLog = nameof(ReceiveLog);
    public const string ReceiveMessage = nameof(ReceiveMessage);
    public const string ReceiveResponse = nameof(ReceiveResponse);
    public const string ReceiveConnectRequest = nameof(ReceiveConnectRequest);
    public const string ReceiveConnectionEstablished = nameof(ReceiveConnectionEstablished);

    public async Task Log(string sender, string senderId, string message)
    {
        await Clients.All.SendAsync(ReceiveLog, sender, senderId, message);
    }

    public async Task SendMessage(string sender, string senderId, string? receiverId, string message, string messageId, string? package)
    {
        if (receiverId is not null)
            await Clients.Client(receiverId).SendAsync(ReceiveMessage, sender, senderId, message, messageId, package);
        else
            await Clients.All.SendAsync(ReceiveMessage, sender, senderId, message, messageId, package);
    }

    public async Task SendResponse(string sender, string senderId, string receiverId, string messageId, string response)
    {
        await Clients.Client(receiverId).SendAsync(ReceiveResponse, sender, senderId, messageId, response);
    }

    public async Task ConnectRequest(string sender, string senderId, string requestId, string target)
    {
        await Clients.All.SendAsync(ReceiveConnectRequest, sender, senderId, requestId, target);
    }

    public async Task ConnectionEstablished(string sender, string senderId, string receiverId, string requestId)
    {
        await Clients.Client(receiverId).SendAsync(ReceiveConnectionEstablished + requestId, senderId, requestId);
    }
}

