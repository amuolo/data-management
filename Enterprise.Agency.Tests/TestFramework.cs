using Enterprise.MessageHub;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Enterprise.Agency.Tests;

public static class TestFramework
{
    public record Log(string Sender, string Message);

    public const string Url = "https://localhost:7158";

    public static WebApplication StartServer()
    {
        var builder = WebApplication.CreateBuilder();

        builder.Services.AddSignalR();

        var workplace = new Workplace(Url) with
        {
            HireAgentsPeriod = TimeSpan.FromMinutes(30),
            OnBoardingWaitingTime = TimeSpan.FromSeconds(1),
            OffBoardingWaitingTime = TimeSpan.FromSeconds(1),
        };

        builder.Services.AddHostedService<Manager>()
                        .AddSingleton(workplace);

        var app = builder.Build();

        app.MapHub<PostingHub>(Addresses.SignalR);

        app.RunAsync(Url);

        return app;
    }

    public static async Task<(WebApplication Server, Office<IAgencyContract> Logger, Office<IContractExample1> Office1, Office<IContractExample2> Office2)>
    SetupThreeBodyProblemAsync(List<Log> storage)
    {
        var server = StartServer();

        var logger = Office<IAgencyContract>.Create(Url)
                        .ReceiveLogs((sender, senderId, message) => storage.Add(new Log(sender, message)))
                        .Run();

        var office1 = Office<IContractExample1>.Create(Url)
                        .Run();

        var office2 = Office<IContractExample2>.Create(Url)
                        .Run();

        await office1.ConnectToAsync(logger.Me);
        await office1.ConnectToAsync(office2.Me);

        await office2.ConnectToAsync(logger.Me);
        await office2.ConnectToAsync(office1.Me);

        return (server, logger, office1, office2);
    }
}
