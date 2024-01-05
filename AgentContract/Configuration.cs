using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Immutable;

namespace Agency;

public class Configuration(WebApplicationBuilder Builder, WebApplication? App, ImmutableList<Type> Hubs)
{
    public static Configuration Create()
    {
        var builder = WebApplication.CreateBuilder();

        builder.Logging.ClearProviders().AddConsole();

        builder.Services.AddRazorPages();
        builder.Services.AddServerSideBlazor();
        builder.Services.AddControllers();
        builder.Services.AddHttpContextAccessor();

        builder.Services.AddSignalR();

        return new Configuration(builder, default, ImmutableList<Type>.Empty);
    }

    public Configuration AddAgent<TState, THub, IContract>()
            where TState : new()
            where THub : Hub<IContract>
            where IContract : class
    {
        Builder.Services.AddHostedService<Agent<TState, THub, IContract>>();
        Hubs.Add(typeof(THub));
        return this;
    }

    public Configuration Run()
    {
        App = Builder.Build();

        App.UseRouting();
        App.UseHttpsRedirection();
        App.UseStaticFiles();
        App.UseWebSockets();

        foreach (var type in Hubs) 
            App.GetType().GetMethod("MapHub", [type])?.Invoke(App, new object[] { "/signalr-messaging" });

        Task.Run(() => App.Run());

        return this;
    }
}
