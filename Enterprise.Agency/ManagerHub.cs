using Enterprise.MessageHub;

namespace Enterprise.Agency;

public class ManagerHub : MessageHub<IAgencyContract>
{
    public async Task<ManagerResponse?> AgentsRegistrationRequest(AgentsToHire agentsToHire, AgencyCulture state)
    {
        var hired = new List<AgentInfo>();
        var validatedRecruits = agentsToHire.Recruits.Where(x => x is not null && state.RegisteredAgents.Contains(x.Name))
                                                     .Select(x => x with { Active = true, LastInteraction = DateTime.Now }).ToList();

        if (validatedRecruits.Any())
        {
            if (state.InfoByActor.TryGetValue(agentsToHire.From, out var info))
                hired = validatedRecruits.Where(agent => !info.Any(x => x.Name == agent.Name && x.Active)).ToList();
            else
                hired = validatedRecruits;

            state.InfoByActor[agentsToHire.From] = validatedRecruits;
        }

        if (hired.Any())
            await Recruitment.RecruitAsync(hired, this, state);

        if (validatedRecruits.Any())
            return new ManagerResponse(true, hired, Id);

        return null;
    }

    public async Task<ManagerResponse> AgentsDiscovery(string from, AgencyCulture state)
    {
        var hired = new List<AgentInfo>();

        if (state.InfoByActor.TryGetValue(from, out var info))
        {
            info.ForEach(x =>
            {
                if (!x.Active)
                    hired.Add(x);
                x.LastInteraction = DateTime.Now;
                x.Active = true;
            });
        }

        if (hired.Any())
            await Recruitment.RecruitAsync(hired, this, state);
            
        return new ManagerResponse(true, hired, Id);
    }
}


