﻿using Enterprise.MessageHub;
using Enterprise.Utils;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

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
               
        var workplace = new Workplace(url) with
        {
            AgentTypes = agents?? [],
            HireAgentsPeriod = TimeSpan.FromMinutes(30),
            OnBoardingWaitingTime = TimeSpan.FromSeconds(1),
            OffBoardingWaitingTime = TimeSpan.FromSeconds(1),
        };

        builder.Services.AddHostedService<Manager>()
                        .AddSingleton(workplace);

        var app = builder.Build();

        app.MapHub<PostingHub>(Addresses.SignalR);

        app.RunAsync(url);

        return app;
    }

    public static async Task<(WebApplication Server, Office<IAgencyContract> Logger, Office<IContractExample1> Office1, Office<IContractExample2> Office2)>
    SetupThreeBodyProblemAsync(List<Log> storage, Type[]? agents = null)
    {
        var server = await StartServerAsync(agents);
        var url = server.Urls.First();

        var logger = Office<IAgencyContract>.Create(url)
                        .ReceiveLogs((sender, senderId, message) => storage.Add(new Log(sender, message)))
                        .Run();

        var office1 = Office<IContractExample1>.Create(url)
                        .Run();

        var office2 = Office<IContractExample2>.Create(url)
                        .Run();

        await office1.ConnectToAsync(logger.Me);
        await office1.ConnectToAsync(office2.Me);

        await office2.ConnectToAsync(logger.Me);
        await office2.ConnectToAsync(office1.Me);

        return (server, logger, office1, office2);
    }

    public static async Task<(WebApplication server, Office<IAgencyContract> logger, Office<IContractAgentX> office, string agent)> 
    SetupLoggerOfficeAgentAsync(List<Log> storage)
    {
        var server = await StartServerAsync([typeof(Agent<XModel, XHub, IContractAgentX>)]);
        var url = server.Urls.First();
        var agentName = typeof(XHub).ExtractName();

        var logger = Office<IAgencyContract>.Create(url)
                        .ReceiveLogs((sender, senderId, message) => storage.Add(new Log(sender, message)))
                        .Run();

        var office = Office<IContractAgentX>.Create(url)
                        .AddAgent<XModel, XHub, IContractAgentX>()
                        .Run();

        await office.ConnectToAsync(logger.Me);
        await office.ConnectToAsync(agentName);

        return (server, logger, office, agentName);
    }
}
