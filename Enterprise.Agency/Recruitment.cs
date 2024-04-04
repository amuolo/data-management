using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Enterprise.Agency;

internal static class Recruitment
{
    internal static WebApplication Recruit((Type Agent, Type Hub) actor)
    {
        var builder = WebApplication.CreateBuilder();

        builder.Logging.ClearProviders().AddConsole();

        builder.Services.AddSignalR();

        builder.Services.AddResponseCompression(opts =>
            opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[] { "application/octet-stream" }));

        // This is the generic variant of builder.Services.AddHostedService<Agent>()
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IHostedService), actor.Agent));

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.RunAsync();
        return app;
    }
}
