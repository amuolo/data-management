using Microsoft.AspNetCore.SignalR;

namespace ServerBlazor.Hubs;

public class ServerHub : Hub
{
    public Task SendMessage(string sender, Guid senderId, string message, object? package = null)
    {
        return Clients.All.SendAsync("ReceiveMessage", sender, senderId, message, package);
    }

    public Task Log(string sender, Guid senderId, string message)
    {
        return Clients.All.SendAsync("ReceiveLog", sender, senderId, message);
    }
}
