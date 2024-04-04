using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;

namespace Enterprise.Agency;

public class HiringState
{
    public ConcurrentDictionary<string, (Type Agent, string Id, DateTime Time, bool Active)> DossierByAgent { get; set; }

    public Dictionary<string, IHost> Hosts { get; set; }
}

public interface IHiringContract
{
    /* in */
    Task GetRequest(string agent);
}

public class HiringHub : MessageHub<IHiringContract>
{
    public async Task<string> GetRequest(string agent, HiringState state)
    {
        state.DossierByAgent.TryGetValue(agent, out var info);

        // TODO: finish this

        return info.Id;
    }
}

