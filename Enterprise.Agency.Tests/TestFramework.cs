using Enterprise.MessageHub;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Enterprise.Agency.Tests;

public static class TestFramework
{
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
}
