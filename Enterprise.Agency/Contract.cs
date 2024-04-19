using Enterprise.MessageHub;

namespace Enterprise.Agency;

public interface IAgencyContract : IHubContract
{
    Task<ManagerResponse> AgentsRegistrationRequest(AgentsDossier dossier);
}

public record AgentsDossier(List<Curriculum> Agents, string From);

public record ManagerResponse(bool Status, List<Curriculum> Hired);


