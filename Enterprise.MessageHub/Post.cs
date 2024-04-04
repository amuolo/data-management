using Enterprise.Agency;
using Enterprise.Utils;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualBasic;
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
            await EstablishConnection(connection).ConfigureAwait(false);
            await ConnectToServer(connection, queue, token).ConfigureAwait(false);

            while (!queue.IsEmpty && !token.IsCancellationRequested)
            {
                var ok = queue.TryDequeue(out var parcel);
                var id = GetId(connection);

                if (!ok || parcel is null)
                {
                    LogPost(queue, "Issue with Outbox dequeuing");
                    return;
                }
                else if (parcel.Type == MessageType.SendMessage || parcel.Type == MessageType.SendResponse)
                {
                    var receiverId = parcel.Address?.ToString();
                    var packet = parcel.Package is not null ? JsonSerializer.Serialize(parcel.Package) : null;
                    LogPost(queue, $"{parcel.Type} {parcel.Message}");
                    await connection.SendAsync(parcel.Type, queue.Name, id, receiverId, parcel.Message, parcel.Id, packet).ConfigureAwait(false);
                }
                else if (parcel.Type == MessageType.Log)
                {
                    await connection.SendAsync(MessageType.Log, queue.Name, id, parcel.Message).ConfigureAwait(false);
                }
            }
        }

        await connection.StopAsync().ConfigureAwait(false);
        await connection.DisposeAsync().ConfigureAwait(false);
    }

    public static void LogPost(SmartQueue<Parcel> queue, string msg)
        => queue.Enqueue(new Parcel(null, null, msg) with { Type = MessageType.Log });

    private static async Task EstablishConnection(HubConnection connection)
    {
        var timerReconnection = new PeriodicTimer(TimeSpans.ServerConnectionAttemptPeriod);

        while (connection is null || connection?.State != HubConnectionState.Connected)
        {
            await timerReconnection.WaitForNextTickAsync().ConfigureAwait(false);
        }
    }

    private static async Task ConnectToServer(HubConnection connection, SmartQueue<Parcel> queue, CancellationToken token)
    {
        var counter = 0;
        var id = Guid.NewGuid();
        var connected = false;
        var timerReconnection = new PeriodicTimer(TimeSpans.ServerConnectionAttemptPeriod);

        var subscription = connection.On(MessageType.ConnectionEstablished, (string sender, string senderId, Guid messageId) => {
            if (messageId == id)
            {
                connected = true;
                timerReconnection.Dispose();
            }
        });

        do
        {
            if(++counter % 10 == 0)
                await connection.SendAsync(MessageType.Log, queue.Name, id, $"Struggling to connect to server, attempt {++counter}").ConfigureAwait(false);
            await connection.SendAsync(MessageType.SendMessage, queue.Name, GetId(connection), null, Messages.ConnectToServer, id, null).ConfigureAwait(false);
            await timerReconnection.WaitForNextTickAsync().ConfigureAwait(false);
        }
        while (!token.IsCancellationRequested && !connected);

        subscription.Dispose();
    }
}
