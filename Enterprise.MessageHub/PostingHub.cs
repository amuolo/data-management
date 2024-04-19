using Microsoft.AspNetCore.SignalR;

namespace Enterprise.MessageHub;

public class PostingHub : Hub
{
    public async Task Log(string sender, string senderId, string message)
    {
        await Clients.All.SendAsync(nameof(IHubContract.ReceiveLog), sender, senderId, message);
    }

    public async Task SendMessage(string sender, string senderId, string? receiverId, string message, string messageId, string? package)
    {
        if (receiverId is not null)
            await Clients.Client(receiverId).SendAsync(nameof(IHubContract.ReceiveMessage), sender, senderId, message, messageId, package);
        else
            await Clients.All.SendAsync(nameof(IHubContract.ReceiveMessage), sender, senderId, message, messageId, package);
    }

    public async Task SendResponse(string sender, string senderId, string receiverId, string messageId, string response)
    {
        await Clients.Client(receiverId).SendAsync(nameof(IHubContract.ReceiveResponse), sender, senderId, messageId, response);
    }

    public async Task ConnectRequest(string sender, string senderId, string requestId, string target)
    {
        await Clients.All.SendAsync(nameof(ConnectRequest), sender, senderId, requestId, target);
    }

    public async Task ConnectionEstablished(string sender, string senderId, string receiverId, string requestId)
    {
        await Clients.Client(receiverId).SendAsync(nameof(ConnectionEstablished) + requestId, senderId, requestId);
    }
}

