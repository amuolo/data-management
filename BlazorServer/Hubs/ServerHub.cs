using Microsoft.AspNetCore.SignalR;

namespace BlazorServer.Hubs;

public class ServerHub : Hub
{
    public Task SendMessage(string user, string message)
    {
        return Clients.All.SendAsync("ReceiveMessage", user, message);
    }
}
