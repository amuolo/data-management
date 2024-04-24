using Enterprise.MessageHub;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Enterprise.Agency.Tests;

public static class TestFramework
{
    public record Log(string Sender, string Message);

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

    public static async Task<WebApplication> StartServerAsync()
    {
        var url = await GetFreeUrlAsync();
        var builder = WebApplication.CreateBuilder();

        builder.Services.AddSignalR();
               
        var workplace = new Workplace(url) with
        {
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
    SetupThreeBodyProblemAsync(List<Log> storage)
    {
        var server = await StartServerAsync();
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
}
