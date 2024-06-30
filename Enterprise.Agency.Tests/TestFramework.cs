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
        return "http://localhost:" + new Random().Next(1, 20000).ToString();
    }

    public static async Task<WebApplication> StartServerAsync()
    {
        var url = await GetFreeUrlAsync();
        var builder = WebApplication.CreateBuilder();

        builder.Services.AddSignalR();

        var app = builder.Build();

        app.MapHub<PostingHub>(Addresses.SignalR);

        app.RunAsync(url);

        return app;
    }

    public static async Task<WebApplication> StartServerWithManagerAsync(Type[]? agents, int agentsPeriod = 1800)
    {
        var url = await GetFreeUrlAsync();
        var builder = WebApplication.CreateBuilder();

        builder.Services.AddSignalR();
               
        builder.Services.AddAgencyManager(url, o => o
            .WithAgentTypes(agents?? [])
            .WithHireAgentsPeriod(TimeSpan.FromSeconds(agentsPeriod))
            .WithOnBoardingWaitingTime(TimeSpan.FromSeconds(1))
            .WithOffBoardingWaitingTime(TimeSpan.FromSeconds(1)));

        var app = builder.Build();

        app.MapHub<PostingHub>(Addresses.SignalR);

        app.RunAsync(url);

        return app;
    }

    public static async Task<(WebApplication Server, Project<IAgencyContract> Logger, Project<IContractExample1> Project1, Project<IContractExample2> Project2, ConcurrentBag<Log> storage)>
    SetupThreeProjectsAsync()
    {
        var server = await StartServerAsync();
        var url = server.Urls.First();
        var storage = new ConcurrentBag<Log>();

        var logger = Project<IAgencyContract>.Create(url)
                        .ReceiveLogs((sender, senderId, message) => storage.Add(new Log(sender, message)))
                        .Run();

        var project1 = Project<IContractExample1>.Create(url).Run();

        var project2 = Project<IContractExample2>.Create(url).Run();

        // Being projects autonomous entities, here we ensure they are both up and running when the test starts
        await project1.ConnectToAsync(logger.Me);
        await project1.ConnectToAsync(project2.Me);
        await project2.ConnectToAsync(project1.Me);
        await project2.ConnectToAsync(logger.Me);

        return (server, logger, project1, project2, storage);
    }

    public static async Task<(WebApplication server, Project<IAgencyContract> logger, Project<IContractAgentX> project, string agent, ConcurrentBag<Log> storage)> 
    SetupManagerAgentProjectLogger(int agentsPeriod = 1800)
    {
        var server = await StartServerWithManagerAsync([typeof(Agent<XModel, XHub, IContractAgentX>)], agentsPeriod);
        var url = server.Urls.First();
        var agentName = typeof(XHub).ExtractName();
        var storage = new ConcurrentBag<Log>();

        var logger = Project<IAgencyContract>.Create(url)
                        .ReceiveLogs((sender, senderId, message) => storage.Add(new Log(sender, message)))
                        .Run();

        var project = Project<IContractAgentX>.Create(url)
                        .AddAgent<XModel, XHub, IContractAgentX>()
                        .Run();

        // While projects are autonomous entities, and here we ensure they are both up and running when the test starts,
        // agents are managed resources, and we don't have to worry about their life time or readiness as this is 
        // entirely handled by the Manager service.
        await project.ConnectToAsync(logger.Me);

        return (server, logger, project, agentName, storage);
    }
}
