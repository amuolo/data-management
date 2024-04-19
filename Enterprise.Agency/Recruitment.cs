using Enterprise.MessageHub;
using Enterprise.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Enterprise.Agency;

internal static class Recruitment
{
    public static async Task RecruitAsync<IContract>(List<Curriculum> hired, MessageHub<IContract> messageHub, Workplace workplace)
    where IContract : class, IAgencyContract
    {
        foreach (var agent in hired)
        {
            var agentType = workplace.AgentTypes.FirstOrDefault(x => x.ExtractName().Contains(agent.Name));
            if (agentType is not null)
            {
                messageHub.LogPost($"hiring {agent.Name}.");
                workplace.Hosts[agent.Name] = Recruit(agentType, workplace);
            }
            else
            {
                messageHub.LogPost($"agent curriculum not found for: {agent.Name}.");
            }
        }
    }

    // TODO Promote this method to publish actors to different physical locations. 
    internal static IHost Recruit(Type agent, Workplace workplace)
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddSignalR();

        // This is the generic variant of builder.Services.AddHostedService<Agent>()
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IHostedService), agent));

        builder.Services.AddSingleton(new Workplace(workplace.Url));

        var host = builder.Build();

        host.RunAsync();

        return host;
    }
}
