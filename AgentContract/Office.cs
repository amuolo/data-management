﻿using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Threading.Channels;

namespace Agency;

public class Office<IContract>(WebApplicationBuilder Builder, WebApplication? App, ImmutableList<Type> Hubs) where IContract : class
{
    private ConcurrentDictionary<Type, object> managers = [];

    Channel<Action<IContract>> ChannelGlobal { get; } = Channel.CreateUnbounded<Action<IContract>>(new UnboundedChannelOptions() { SingleReader = false });
    ChannelReader<Action<IContract>> ChannelReader => ChannelGlobal.Reader;
    ChannelWriter<Action<IContract>> ChannelWriter => ChannelGlobal.Writer;

    public static Office<IContract> Create()
    {
        var builder = WebApplication.CreateBuilder();

        var office = new Office<IContract>(builder, default, []);

        builder.Logging.ClearProviders().AddConsole();

        builder.Services.AddRazorPages();
        builder.Services.AddServerSideBlazor();
        builder.Services.AddControllers();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSignalR();

        builder.Services.AddSingleton(office.ChannelGlobal);
        builder.Services.AddHostedService<Manager<IContract>>();
        
        /* For reference
        Host.CreateDefaultBuilder()
            .ConfigureServices(services => services.AddSingleton(office.ChannelGlobal)
                                                   .AddHostedService<Manager<IContract>>()
                                                   .AddSignalR()).Build().Run(); */

        return office;
    }

    public Office<IContract> AddAgent<TState, THub, IHubContract>()
            where TState : new()
            where THub : Hub<IHubContract>
            where IHubContract : class
    {
        Builder.Services.AddHostedService<Agent<TState, THub, IHubContract>>();
        Hubs.Add(typeof(THub));
        return this;
    }

    public Office<IContract> Run()
    {
        App = Builder.Build();

        App.UseRouting();
        App.UseHttpsRedirection();
        App.UseStaticFiles();
        App.UseWebSockets();

        App.MapHub<ManagerHub<IContract>>(Navigator.SignalRAddress);

        foreach (var type in Hubs) 
            App.GetType().GetMethod("MapHub", [type])?.Invoke(App, new object[] { Navigator.SignalRAddress });

        App.RunAsync();

        return this;
    }

    public void Post(Action<IContract> action)
    {
        Task.Run(async () => await ChannelWriter.WriteAsync(action));
    }
}
