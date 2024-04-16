using Enterprise.MessageHub;

namespace Enterprise.Agency;

public interface IAgencyContract : IHubContract
{
    Task<ManagerResponse> AgentsDiscoveryRequest(AgentsDossier dossier);
}

public record AgentsDossier(List<Curriculum> Agents, string From);

public record ManagerResponse(bool Status);


