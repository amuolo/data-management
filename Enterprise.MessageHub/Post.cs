using Enterprise.Agency;
using Enterprise.Utils;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;
using System.Text.Json;

namespace Enterprise.MessageHub;

public class Post : BackgroundService
{
    private SmartQueue<Parcel> Queue { get; set; }

    public HubConnection Connection { get; }

    public static string GetId(HubConnection connection)
    {
        if (connection is null || connection.ConnectionId is null)
            throw new ArgumentNullException("Connection id found null.");
        return connection.ConnectionId;
    }

    public Post(SmartQueue<Parcel> smartQueue, HubConnection hubConnection)
    {
        Connection = hubConnection;
        Queue = smartQueue;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await StartMessageServiceAsync(Queue, Connection, stoppingToken);
    }

    public static async Task StartMessageServiceAsync(SmartQueue<Parcel> queue, HubConnection connection, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await queue.Semaphore.WaitAsync(token).ConfigureAwait(false);
            await EstablishConnectionAsync(connection).ConfigureAwait(false);
            await ConnectToServerAsync(connection, queue.Name, token).ConfigureAwait(false);

            while (!queue.IsEmpty && !token.IsCancellationRequested)
            {
                var ok = queue.TryDequeue(out var parcel);
                var id = GetId(connection);

                if (!ok || parcel is null)
                {
                    LogPost(queue, "Issue with Outbox dequeuing");
                    return;
                }
                else if (parcel.Type == MessageTypes.SendMessage || parcel.Type == MessageTypes.SendResponse)
                {
                    var receiverId = parcel.Address?.ToString();
                    var packet = parcel.Package is not null ? JsonSerializer.Serialize(parcel.Package) : null;
                    LogPost(queue, $"{parcel.Type} {parcel.Message}");
                    await connection.SendAsync(parcel.Type, queue.Name, id, receiverId, parcel.Message, parcel.Id, packet).ConfigureAwait(false);
                }
                else if (parcel.Type == MessageTypes.Log)
                {
                    await connection.SendAsync(MessageTypes.Log, queue.Name, id, parcel.Message).ConfigureAwait(false);
                }
            }
        }

        await connection.StopAsync().ConfigureAwait(false);
        await connection.DisposeAsync().ConfigureAwait(false);
    }

    public static void LogPost(SmartQueue<Parcel> queue, string msg)
        => queue.Enqueue(new Parcel(null, null, msg) with { Type = MessageTypes.Log });

    public static async Task EstablishConnectionAsync(HubConnection connection)
    {
        var timerReconnection = new PeriodicTimer(TimeSpans.ServerConnectionAttemptPeriod);

        while (connection is null || connection?.State != HubConnectionState.Connected)
        {
            await timerReconnection.WaitForNextTickAsync().ConfigureAwait(false);
            // TODO: add log
        }
    }

    public static async Task ConnectToServerAsync(HubConnection connection, string name, CancellationToken token)
    {
        var counter = 0;
        var id = Guid.NewGuid();
        var connected = false;
        var timerReconnection = new PeriodicTimer(TimeSpans.ServerConnectionAttemptPeriod);

        var subscription = connection.On(MessageTypes.ConnectionEstablished, (string sender, string senderId, Guid messageId) => {
            if (messageId == id)
            {
                connected = true;
                timerReconnection.Dispose();
            }
        });

        do
        {
            if(++counter % 10 == 0)
            {
                var msg = $"{name} struggling to connect to server, attempt {++counter}";
                await connection.SendAsync(MessageTypes.Log, name, id, msg).ConfigureAwait(false);
                // TODO: add log
            }
            await connection.SendAsync(MessageTypes.SendMessage, name, GetId(connection), null, Messages.ConnectToServer, id, null).ConfigureAwait(false);
            await timerReconnection.WaitForNextTickAsync().ConfigureAwait(false);
        }
        while (!token.IsCancellationRequested && !connected);

        subscription.Dispose();
    }
}
