using Enterprise.MessageHub;
using Enterprise.Utils;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace Enterprise.Agency.Tests;

public static class TestFramework
{
    public static async Task<string> GetFreeUrlAsync()
    {
        /*
        string url = string.Empty;
        var lh = "https://localhost:";
        var i = 2000;

        while (true)
        {
            try
            {
                var uri = lh + i.ToString();
                HttpClient client = new HttpClient();
                var r = await client.GetAsync(uri);
            }
            catch
            {
                url = lh + i.ToString();
                break;
            }
            finally
            {
                i++;
            }
        }

        return url;
        */
        return "https://localhost:" + new Random().Next(1, 9999).ToString();
    }

    public static async Task<WebApplication> StartServerAsync(Type[]? agents)
    {
        var url = await GetFreeUrlAsync();
        var builder = WebApplication.CreateBuilder();

        builder.Services.AddSignalR();
               
        builder.Services.AddAgencyServices(url, o => o
            .WithAgentTypes(agents?? [])
            .WithHireAgentsPeriod(TimeSpan.FromMinutes(30))
            .WithOnBoardingWaitingTime(TimeSpan.FromSeconds(1))
            .WithOffBoardingWaitingTime(TimeSpan.FromSeconds(1)));

        var app = builder.Build();

        app.MapHub<PostingHub>(Addresses.SignalR);

        app.RunAsync(url);

        return app;
    }

    public static async Task<(WebApplication Server, Project<IAgencyContract> Logger, Project<IContractExample1> Project1, Project<IContractExample2> Project2)>
    SetupThreeProjectsAsync(ConcurrentBag<Log> storage, Type[]? agents = null)
    {
        var server = await StartServerAsync(agents);
        var url = server.Urls.First();

        var logger = Project<IAgencyContract>.Create(url)
                        .ReceiveLogs((sender, senderId, message) => storage.Add(new Log(sender, message)))
                        .Run();

        var project1 = Project<IContractExample1>.Create(url).Run();

        var project2 = Project<IContractExample2>.Create(url).Run();

        await project1.ConnectToAsync(logger.Me);
        await project1.ConnectToAsync(project2.Me);

        await project2.ConnectToAsync(logger.Me);
        await project2.ConnectToAsync(project1.Me);

        return (server, logger, project1, project2);
    }

    public static async Task<(WebApplication server, Project<IAgencyContract> logger, Project<IContractAgentX> project, string agent)> 
    SetupLoggerProjectAgentAsync(ConcurrentBag<Log> storage)
    {
        var server = await StartServerAsync([typeof(Agent<XModel, XHub, IContractAgentX>)]);
        var url = server.Urls.First();
        var agentName = typeof(XHub).ExtractName();

        var logger = Project<IAgencyContract>.Create(url)
                        .ReceiveLogs((sender, senderId, message) => storage.Add(new Log(sender, message)))
                        .Run();

        var project = Project<IContractAgentX>.Create(url)
                        .AddAgent<XModel, XHub, IContractAgentX>()
                        .Run();

        await project.ConnectToAsync(logger.Me);
        await project.ConnectToAsync(agentName);

        return (server, logger, project, agentName);
    }
}
