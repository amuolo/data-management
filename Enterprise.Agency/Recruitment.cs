using Enterprise.MessageHub;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Enterprise.Agency;

internal static class Recruitment
{
    // TODO Promote this method to publish actors to different physical locations. 
    internal static IHost Recruit((Type Agent, Type Hub) actor)
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Logging.ClearProviders().AddConsole();

        builder.Services.AddSignalR();

        // This is the generic variant of builder.Services.AddHostedService<Agent>()
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IHostedService), actor.Agent));

        var host = builder.Build();

        host.RunAsync();

        return host;
    }
}
