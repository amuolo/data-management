using Enterprise.MessageHub;
using Enterprise.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Enterprise.Agency;

internal static class Recruitment
{
    public static async Task RecruitAsync<IContract>(List<Curriculum> hired, MessageHub<IContract> messageHub, Dictionary<string, IHost> hosts, Type[] agentTypes)
    where IContract : class, IAgencyContract
    {
        foreach (var agent in hired)
        {
            var agentType = agentTypes.FirstOrDefault(x => x.ExtractName().Contains(agent.Name));
            if (agentType is not null)
            {
                hosts[agent.Name] = Recruit(agentType);
                // TODO: add a timeout
                await Post.ConnectToAsync(messageHub.Connection, messageHub.Me, messageHub.Id, agent.Name, default);
                messageHub.LogPost($"hiring {agent.Name}.");
            }
            else
            {
                messageHub.LogPost($"agent curriculum not found for: {agent.Name}.");
            }
        }
    }

    // TODO Promote this method to publish actors to different physical locations. 
    internal static IHost Recruit(Type agent)
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Logging.ClearProviders().AddConsole();

        builder.Services.AddSignalR();

        // This is the generic variant of builder.Services.AddHostedService<Agent>()
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IHostedService), agent));

        var host = builder.Build();

        host.RunAsync();

        return host;
    }
}
