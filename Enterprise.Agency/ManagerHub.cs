using Enterprise.MessageHub;

namespace Enterprise.Agency;

public class ManagerHub : MessageHub<IAgencyContract>
{
    public async Task<ManagerResponse> AgentsRegistrationRequest(AgentsDossier dossier, Workplace state)
    {
        var hired = new List<Curriculum>();

        if (dossier.Agents is not null)
        {
            var agents = dossier.Agents.Where(x => x is not null).ToList()!;

            if (state.DossierByActor.TryGetValue(dossier.From, out var archive))
            {
                agents.ForEach(agent =>
                {
                    if (!archive.Any(x => x.AgentInfo.Name == agent.Name))
                    {
                        archive.Add((agent, DateTime.Now, true));
                        hired.Add(agent);
                    }
                });
            }
            else
            {
                state.DossierByActor[dossier.From] = agents.Select(x => (x, DateTime.Now, true)).ToList();
                hired = agents;
            }
        }
        else
        {
            LogPost($"dossier agents found null.");
        }

        if (hired.Any())
            await Recruitment.RecruitAsync(hired, this, state);
        return new ManagerResponse(true, hired);
    }

    public async Task<ManagerResponse> AgentsDiscovery(string from, Workplace state)
    {
        var hired = new List<Curriculum>();

        if (state.DossierByActor.TryGetValue(from, out var archive))
        {
            archive.ForEach(x =>
            {
                if (!x.Active)
                    hired.Add(x.AgentInfo);
                x.Active = true;
                x.Time = DateTime.Now;
            });
        }

        if (hired.Any())
            await Recruitment.RecruitAsync(hired, this, state);
        return new ManagerResponse(true, hired);
    }
}


