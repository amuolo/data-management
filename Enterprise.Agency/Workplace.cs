using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;

namespace Enterprise.Agency;

public record Workplace(string Url)
{
    public ConcurrentDictionary<string, List<(Curriculum AgentInfo, DateTime Time, bool Active)>> DossierByActor { get; set; } = [];

    public Dictionary<string, IHost> Hosts { get; set; } = [];

    public Type[] AgentTypes { get; set; } = [];

    public TimeSpan HireAgentsPeriod { get; set; } = TimeSpan.FromMinutes(30);

    public TimeSpan OnBoardingWaitingTime { get; set; } = TimeSpan.FromSeconds(1);

    public TimeSpan OffBoardingWaitingTime { get; set; } = TimeSpan.FromSeconds(1);

    public Workplace() : this("") { }
}
