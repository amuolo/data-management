using Microsoft.AspNetCore.SignalR;

namespace ServerBlazor.Hubs;

public class ServerHub : Hub
{
    public Task SendMessage(string sender, string message, object? package = null)
    {
        return Clients.All.SendAsync("ReceiveMessage", sender, message, package);
    }
}
