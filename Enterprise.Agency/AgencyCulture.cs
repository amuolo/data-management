using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;

namespace Enterprise.Agency;

public record AgencyCulture(string Url = "")
{
    public ConcurrentDictionary<string, List<(Curriculum AgentInfo, DateTime Time, bool Active)>> DossierByActor { get; } = [];

    public Dictionary<string, IHost> Hosts { get; } = [];

    public Type[] AgentTypes { get; private set; } = [];

    public TimeSpan HireAgentsPeriod { get; private set; } = TimeSpan.FromMinutes(30);

    public TimeSpan OnBoardingWaitingTime { get; private set; } = TimeSpan.FromSeconds(1);

    public TimeSpan OffBoardingWaitingTime { get; private set; } = TimeSpan.FromSeconds(1);

    public AgencyCulture WithAgentTypes(Type[] types) => this with { AgentTypes = types };

    public AgencyCulture WithHireAgentsPeriod(TimeSpan time) => this with { HireAgentsPeriod = time };

    public AgencyCulture WithOnBoardingWaitingTime(TimeSpan time) => this with { OnBoardingWaitingTime = time };

    public AgencyCulture WithOffBoardingWaitingTime(TimeSpan time) => this with { OffBoardingWaitingTime = time };
}
