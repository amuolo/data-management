using Enterprise.MessageHub;

namespace Enterprise.Agency;

public interface IAgencyContract : IHubContract
{
    Task<ManagerResponse?> AgentsRegistrationRequest(AgentsToHire dossier);

    TState ReadRequest<TState>();
}

public record AgentsToHire(List<AgentInfo> Recruits, string From);

public record ManagerResponse(bool Status, List<AgentInfo> Hired, string ManagerAddress);

public record AgentInfo(string Name, string AssemblyQualifiedName, string MessageHubName)
{
    public bool Active { get; set; }

    public DateTime LastInteraction { get; set; }
}


