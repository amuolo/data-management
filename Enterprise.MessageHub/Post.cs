using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;

namespace Enterprise.MessageHub;

public class Post : BackgroundService
{
    private Equipment Equipment { get; set; }

    public Post(Equipment equipment)
    {
        Equipment = equipment;
    }

    protected override async Task ExecuteAsync(CancellationToken token)
    {
        await StartMessageServiceAsync(Equipment, token);
    }

    public static async Task StartMessageServiceAsync(Equipment info, CancellationToken token)
    {
        var me = info.Name;
        var store = info.SmartStore;
        var connection = info.HubConnection;

        while (!token.IsCancellationRequested)
        {
            await store.Semaphore.WaitAsync(token).ConfigureAwait(false);

            while (!store.IsEmpty && !token.IsCancellationRequested)
            {
                var status = false;
                try
                {
                    var ok = store.TryPeek(out var parcel);
                    var id = await EstablishConnectionAsync(connection, token).ConfigureAwait(false);

                    if (!ok || parcel is null)
                    {
                        LogPost(info, "Issue with Outbox dequeuing");
                    }
                    else if (parcel.Type == nameof(PostingHub.SendMessage))
                    {
                        LogPost(info, $"{parcel.Type} {parcel.Message}");
                        var target = parcel.Target?.ToString();
                        var targetId = target is null ? null : await ConnectToAsync(connection, me, target, token).ConfigureAwait(false);
                        if (targetId is null) await ConnectToAsync(connection, me, Addresses.Central, token).ConfigureAwait(false);
                        var package = parcel.Item is not null ? JsonConvert.SerializeObject(parcel.Item) : null;      
                        await connection.SendAsync(parcel.Type, me, id, targetId, parcel.Message, parcel.Id, package).ConfigureAwait(false);
                        status = true;
                    }
                    else if (parcel.Type == nameof(PostingHub.SendResponse))
                    {
                        LogPost(info, $"{parcel.Type} {parcel.Message}");
                        var package = parcel.Item is not null ? JsonConvert.SerializeObject(parcel.Item) : null;
                        await ConnectToAsync(connection, me, Addresses.Central, token).ConfigureAwait(false);
                        await connection.SendAsync(parcel.Type, me, id, parcel.TargetId, parcel.Id, package).ConfigureAwait(false);
                        status = true;
                    }
                    else if (parcel.Type == nameof(PostingHub.Log))
                    {
                        if(me != Addresses.Logger)
                            await ConnectToAsync(connection, me, Addresses.Logger, token).ConfigureAwait(false);
                        await connection.SendAsync(nameof(PostingHub.Log), me, id, parcel.Message).ConfigureAwait(false);
                        status = true;
                    }
                }
                catch (Exception ex)
                {
                    LogPost(info, ex.Message);
                }
                finally
                {
                    if(status)
                        store.TryDequeue(out _);
                }
            }
        }

        await connection.StopAsync().ConfigureAwait(false);
        await connection.DisposeAsync().ConfigureAwait(false);
    }

    public static void LogPost(Equipment info, string msg)
        => info.SmartStore.Enqueue(new Parcel(default, default, default, msg) with { Type = nameof(PostingHub.Log) });

    public static async Task<string> EstablishConnectionAsync(HubConnection connection, CancellationToken token)
    {
        using (var timerReconnection = new PeriodicTimer(TimeSpans.ActorConnectionAttemptPeriod))
        {
            while ((connection is null || connection.State != HubConnectionState.Connected || connection.ConnectionId is null) && !token.IsCancellationRequested)
            {
                await timerReconnection.WaitForNextTickAsync().ConfigureAwait(false);
                // TODO: add log
            }
        }

        return connection!.ConnectionId!;
    }

    public static async Task<string> ConnectToAsync(HubConnection connection, string from, string target, CancellationToken token)
    {
        var id = await EstablishConnectionAsync(connection, token).ConfigureAwait(false);

        var counter = 0;
        var targetId = "";
        var connected = false;
        var requestId = Guid.NewGuid().ToString();
        var timerReconnection = new PeriodicTimer(TimeSpans.ActorConnectionAttemptPeriod);

        var subscription = connection.On(nameof(PostingHub.ConnectionEstablished) + requestId, 
            (string senderId, string messageId) => {
                if (messageId == requestId)
                {
                    targetId = senderId;
                    connected = true;
                    timerReconnection.Dispose();
                }
                else
                {
                    throw new Exception("Failing to establish safe connection.");
                }
        });

        do
        {
            if (++counter % 10 == 0)
            {
                var msg = $"Struggling to connect to {target}, attempt {++counter}";
                await connection.SendAsync(nameof(PostingHub.Log), from, requestId, msg).ConfigureAwait(false);
                // TODO: add log
            }
            await connection.SendAsync(nameof(PostingHub.ConnectRequest), from, id, requestId, target).ConfigureAwait(false);
            await timerReconnection.WaitForNextTickAsync().ConfigureAwait(false);
        }
        while (!token.IsCancellationRequested && !connected);

        subscription.Dispose();
        return targetId;
    }
}
