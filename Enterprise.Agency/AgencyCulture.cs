using Enterprise.MessageHub;
using Enterprise.Utils;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;

namespace Enterprise.Agency;

public record AgencyCulture(string Url = "")
{
    public ConcurrentDictionary<string, List<AgentInfo>> InfoByActor { get; } = [];

    public Dictionary<string, IHost> Hosts { get; } = [];

    public Type[] AgentTypes { get; private set; } = [];

    public TimeSpan HireAgentsPeriod { get; private set; } = TimeSpan.FromMinutes(30);

    public TimeSpan OnBoardingWaitingTime { get; private set; } = TimeSpan.FromSeconds(1);

    public TimeSpan OffBoardingWaitingTime { get; private set; } = TimeSpan.FromSeconds(1);

    public string[] RegisteredAgents { get; private set; } = [];

    public HubConnection HubConnection { get; } = new HubConnectionBuilder().WithUrl(Url + Addresses.SignalR).WithAutomaticReconnect().Build();

    // Public API
    public AgencyCulture WithAgentTypes(Type[] types) => this with { AgentTypes = types, RegisteredAgents = RegisteredAgents.Concat(types.Select(x => x.ExtractName())).ToArray() };

    public AgencyCulture WithHireAgentsPeriod(TimeSpan time) => this with { HireAgentsPeriod = time };

    public AgencyCulture WithOnBoardingWaitingTime(TimeSpan time) => this with { OnBoardingWaitingTime = time };

    public AgencyCulture WithOffBoardingWaitingTime(TimeSpan time) => this with { OffBoardingWaitingTime = time };
}
