using Enterprise.MessageHub;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;

namespace Enterprise.Agency;

public record AgentsDossier(List<(Type Agent, Type Hub)> Actors, string FromOffice);

public record RecruiterResponse;

public class HiringState
{
    public ConcurrentDictionary<string, List<(Type Agent, Type Hub, DateTime Time, bool Active)>> DossierByOffice { get; set; } = [];

    public Dictionary<string, IHost> Hosts { get; set; } = [];
}

public interface IHiringContract
{
    /* in */
    Task<RecruiterResponse> AgentsDiscoveryRequest(AgentsDossier dossier);
}

public class HiringHub : MessageHub<IHiringContract>
{
    public async Task<RecruiterResponse> AgentsDiscoveryRequest(AgentsDossier dossier, HiringState state)
    {
        var hired = new List<(Type Agent, Type Hub)>();

        if (dossier.Actors is not null)
        {
            if (state.DossierByOffice.TryGetValue(dossier.FromOffice, out var info))
            {
                dossier.Actors.ForEach(actor =>
                {
                    if (!info.Any(x => x.Agent == actor.Agent))
                    {
                        info.Add((actor.Agent, actor.Hub, DateTime.Now, true));
                        hired.Add(actor);
                    }
                });
            }
            else
            {
                state.DossierByOffice[dossier.FromOffice] = dossier.Actors.Select(x => (x.Agent, x.Hub, DateTime.Now, true)).ToList();
                hired = dossier.Actors;
            }
        }
        else
        {
            if (state.DossierByOffice.TryGetValue(dossier.FromOffice, out var info))
            {
                info.ForEach(x => 
                { 
                    if(!x.Active)
                        hired.Add((x.Agent, x.Hub));
                    x.Active = true;
                    x.Time = DateTime.Now;
                });
            }
            else
            {
                LogPost($"{GetType().Name} received {nameof(AgentsDiscoveryRequest)} from unknown office: {dossier.FromOffice}.");
            }
        }

        foreach (var actor in hired)
        {
            state.Hosts[actor.Agent.Name] = Recruitment.Recruit(actor);
            await MessageHub.Post.ConnectToActorAsync(Connection, GetType().Name, actor.Agent.Name, default);
        }
                
        return new RecruiterResponse();
    }
}

public class Recruiter : Agent<HiringState, HiringHub, IHiringContract>
{
    public Recruiter(IHubContext<ServerHub> hubContext) : base(hubContext)
    {
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await base.ExecuteAsync(cancellationToken);
    }
}

