using Enterprise.MessageHub;
using Enterprise.Utils;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Enterprise.Agency;

internal static class Recruitment
{
    public static async Task RecruitAsync<IContract>(List<AgentInfo> hired, MessageHub<IContract> messageHub, AgencyCulture culture)
    where IContract : class, IAgencyContract
    {
        await culture.HubConnection.StartAsync();
        foreach (var agent in hired)
        {
            var agentType = culture.AgentTypes.FirstOrDefault(x => x.ExtractName() == agent.Name);
            if (agentType is not null)
            {
                messageHub.LogPost($"hiring {agent.Name}.");
                culture.Hosts[agent.Name] = Recruit(agentType, culture);
                if(!await ConnectToNoviceAsync(culture.HubConnection, agent))
                    messageHub.LogPost($"Failed to start {agent.Name}"); 
            }
            else
            {
                messageHub.LogPost($"agent curriculum not found for: {agent.Name}.");
            }
        }
        await culture.HubConnection.StopAsync();
    }

    private static IHost Recruit(Type agent, AgencyCulture culture)
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddSignalR();

        // This is the generic variant of builder.Services.AddHostedService<Agent>()
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IHostedService), agent));

        builder.Services.AddSingleton(new AgencyCulture(culture.Url));

        var host = builder.Build();

        host.RunAsync();

        return host;
    }

    private static async Task<bool> ConnectToNoviceAsync(HubConnection connection, AgentInfo agent) 
    {
        var cancellation = new CancellationTokenSource();
        cancellation.CancelAfter(TimeSpans.ConnectionTimeout);
        
        await Post.ConnectToAsync(cancellation.Token, connection, Addresses.Central, agent.MessageHubName, null);

        return !cancellation.IsCancellationRequested;
    }
}
