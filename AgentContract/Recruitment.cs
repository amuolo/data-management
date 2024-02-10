using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

        // This is the generic variant of builder.Services.AddHostedService<Agent>()
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IHostedService), actor.Agent));

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

        // TODO: This fails because generic methods in MessageHub are not compatible with Hubs
        // This is the generic variant of app.MapHub<Hub>(address)
        //typeof(HubEndpointRouteBuilderExtensions)
        //    .GetMethod("MapHub", [typeof(IEndpointRouteBuilder), typeof(string)])
        //    .MakeGenericMethod(actor.Hub).Invoke(null, [app, Consts.SignalRAddress]);

        app.RunAsync();  // TODO: consider adding an explicit url

        return app;
    }
}
