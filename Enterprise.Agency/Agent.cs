using Enterprise.Job;
using Enterprise.MessageHub;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System.Reflection;

namespace Enterprise.Agency;

public class Agent<TState, THub, IContract> : BackgroundService
    where THub : MessageHub<IContract>, new()
    where IContract : class, IAgencyContract
{
    protected IHubContext<PostingHub> ServerHub { get; }
    protected THub MessageHub { get; set; }
    protected bool IsInitialized { get; set; }
    protected string Me => MessageHub.Me;

    protected Job<TState> Job { get; set; }

    protected Dictionary<string, MethodInfo> MethodsByName { get; } 
        = typeof(THub).GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance).ToDictionary(x => x.Name);

    public Agent(IHubContext<PostingHub> hubContext, AgencyCulture workplace)
    {
        ServerHub = hubContext;
        MessageHub = MessageHub<IContract>.Create<THub>(workplace.Url);
        Job = JobFactory.New<TState>().WithOptions(o => o.WithLogs(MessageHub.LogPost));
    }

    public override void Dispose()
    {
        MessageHub.LogPost($"disposing");
        MessageHub.Dispose();
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken token)
    {
        await MessageHub.InitializeConnectionAsync(token, ActionMessageReceived);

        var info = new Equipment(Me, MessageHub.Queue, MessageHub.Connection);

        await Post.StartMessageServiceAsync(info, token);
    }

    protected async Task CreateAsync()
    {
        if (!IsInitialized)
        {
            if (MethodsByName.TryGetValue(nameof(IHubContract.CreateRequest), out var create))
            {
                MessageHub.LogPost("Creating myself");
                TState? state = default;
                var r = create.Invoke(MessageHub, null);

                if (r is null)
                {
                    MessageHub.LogPost($"null state retrieved after creation.");
                }
                else if (create.ReturnType == typeof(Task<TState>))
                {
                    state = await (Task<TState>)r;
                }
                else if (create.ReturnType == typeof(TState))
                {
                    state = (TState)r;
                }
                else
                {
                    MessageHub.LogPost($"Incorrect Return Type method {nameof(IHubContract.CreateRequest)}");
                }

                if (state is not null)
                {
                    Job = await Job.WithOptions(o => o.WithLogs(MessageHub.LogPost))
                                   .WithStep(nameof(IHubContract.CreateRequest), s => state)
                                   .StartAsync();
                }
            }

            IsInitialized = true;
        }
    }

    protected async Task ActionMessageReceived(string sender, string senderId, string message, string messageId, string? package)
    {
        if (message == nameof(IHubContract.DeleteRequest) && sender == Addresses.Central)
        {
            Dispose();
            return;
        }
        else if (message == nameof(IAgencyContract.ReadRequest))
        {
            // TODO: check whether this mechanism handles read requests in parallel
            await CreateAsync();
            MessageHub.LogPost($"processing {message}");
            MessageHub.Queue.Enqueue(new Parcel(sender, senderId, Job.State, message) 
                with { Type = nameof(PostingHub.SendResponse), Id = messageId });
            return;
        }
        else if (MethodsByName.TryGetValue(message, out var method))
        {
            // TODO: consider re-using the project registration pattern instead of inventing a new one here
            await CreateAsync();
            MessageHub.LogPost($"processing {message}");

            await Job.WithStep($"{message}", async state =>
            {
                var parameters = method.GetParameters().Select(p => p.ParameterType == typeof(TState) ? state :
                                    (package is not null ? JsonConvert.DeserializeObject(package, p.ParameterType) : null)).ToArray();

                var res = method.Invoke(MessageHub, parameters);
                var returnType = method.ReturnType;
                var err = $"Inconsistent return type found when agent {Me} processed {message}";

                if (returnType == typeof(Task))
                {
                    if (res is null)
                        throw new Exception(err);
                    await (Task)res;
                }
                else if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    if (res is null)
                        throw new Exception(err);
                    await (Task)res;
                    var result = res.GetType().GetProperty("Result")?.GetValue(res);
                    if (result is null)
                        return;
                    MessageHub.Queue.Enqueue(new Parcel(sender, senderId, result, message) 
                        with { Type = nameof(PostingHub.SendResponse), Id = messageId });
                }
                else if (returnType != typeof(void))
                {
                    if (res is null)
                        return;
                    MessageHub.Queue.Enqueue(new Parcel(sender, senderId, res, message)
                        with { Type = nameof(PostingHub.SendResponse), Id = messageId });
                }
            })
            .StartAsync();
            return;
        }
    }
}
