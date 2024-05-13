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
            if (token.IsCancellationRequested) break;

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
                    else if (parcel.Type == nameof(PostingHub.Log))
                    {
                        if(me != Addresses.Logger)
                            await ConnectToAsync(token, connection, me, Addresses.Logger, null).ConfigureAwait(false);
                        await connection.SendAsync(nameof(PostingHub.Log), me, id, parcel.Message).ConfigureAwait(false);
                        status = true;
                    }
                    else
                    {
                        LogPost(info, $"{parcel.Type} {parcel.Message}");
                        var isResponse = parcel.Type == nameof(PostingHub.SendResponse);
                        var targetId = parcel.TargetId;

                        var package = parcel.Item is null ? null
                            : JsonConvert.SerializeObject(parcel.Item, new JsonSerializerSettings
                            {
                                Formatting = Formatting.Indented
                            });

                        if (info.ServiceDiscovery.Active && info.ServiceDiscovery.ConnectAsync is not null)
                            await info.ServiceDiscovery.ConnectAsync().ConfigureAwait(false);

                        if (!isResponse)
                        {
                            var target = parcel.Target?.ToString();
                            targetId = target is null ? null : await ConnectToAsync(token, connection, me, target, null).ConfigureAwait(false);
                        }

                        if (isResponse)
                            await connection.SendAsync(parcel.Type, me, id, targetId, parcel.Id, package).ConfigureAwait(false);
                        else
                            await connection.SendAsync(parcel.Type, me, id, targetId, parcel.Message, parcel.Id, package).ConfigureAwait(false);
                        
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
        if (connection is null)
        {
            // TODO: add log
            throw new ArgumentNullException(nameof(EstablishConnectionAsync) + nameof(connection));
        }

        if (connection.State == HubConnectionState.Disconnected)
            await connection.StartAsync(token).ConfigureAwait(false);

        using (var timerReconnection = new PeriodicTimer(TimeSpans.ActorConnectionAttemptPeriod))
        {
            while ((connection.State != HubConnectionState.Connected || connection.ConnectionId is null) && !token.IsCancellationRequested)
            {
                await timerReconnection.WaitForNextTickAsync(token).ConfigureAwait(false);
                // TODO: add log
                if (token.IsCancellationRequested) break;
            }
        }

        return connection!.ConnectionId!;
    }

    public static async Task<string> ConnectToAsync(CancellationToken token, HubConnection connection, string from, string target, string? targetId)
    {
        var counter = 0;
        var requestId = Guid.NewGuid().ToString();
        var timerReconnection = new PeriodicTimer(TimeSpans.ActorConnectionAttemptPeriod);
        var id = await EstablishConnectionAsync(connection, token).ConfigureAwait(false);

        var subscription = connection.On(PostingHub.ReceiveConnectionEstablished + requestId, 
            (string senderId, string messageId) => {
                if (messageId == requestId && (targetId is null || targetId == senderId))
                {
                    targetId = senderId;
                    timerReconnection.Dispose();
                }
        });

        do
        {
            if (++counter % 10 == 0)
            {
                var msg = $"Struggling to connect to {target}, attempt {++counter}";
                await connection.SendAsync(nameof(PostingHub.Log), from, id, msg).ConfigureAwait(false);
                // TODO: add log
            }
            await connection.SendAsync(nameof(PostingHub.ConnectRequest), from, id, requestId, target).ConfigureAwait(false);
            await timerReconnection.WaitForNextTickAsync().ConfigureAwait(false);
        }
        while (!token.IsCancellationRequested && targetId is null);

        subscription.Dispose();
        return targetId!;
    }
}
