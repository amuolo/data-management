using Microsoft.AspNetCore.SignalR;

namespace ServerBlazor.Hubs;

public class ServerHub : Hub
{
    public Task SendMessage(string user, string message)
    {
        return Clients.All.SendAsync("ReceiveMessage", user, message);
    }
}
