using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agency;

internal static class Recruitment
{
    internal static WebApplication Recruit((Type Agent, Type Hub) actor)
    {
        var builder = WebApplication.CreateBuilder();

        builder.Logging.ClearProviders().AddConsole();

        builder.Services.AddRazorPages();
        builder.Services.AddRazorComponents();
        builder.Services.AddServerSideBlazor();
        builder.Services.AddSignalR();

        builder.Services.AddResponseCompression(opts =>
            opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[] { "application/octet-stream" }));

        builder.Services.GetType().GetMethod("AddHostedService", [actor.Agent])?.Invoke(builder.Services, null);

        //builder.Services.AddHostedService<Agent<TState, THub, IHubContract>>();

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseRouting();
        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.MapBlazorHub();

        app.GetType().GetMethod("MapHub", [actor.Hub])?.Invoke(app, new object[] { Consts.SignalRAddress });

        app.RunAsync();  // TODO: consider adding an explicit url

        return app;
    }
}
