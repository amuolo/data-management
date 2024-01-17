using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Immutable;
using System.Linq.Expressions;
using System.Reflection;

namespace Agency;

public class Office<IContract>(WebApplicationBuilder Builder, WebApplication? App) 
    where IContract : class
{
    private List<Type> Hubs { get; } = [];

    private HubConnection Connection { get; } = new HubConnectionBuilder().WithUrl(SignalR.Url).WithAutomaticReconnect().Build();

    bool IsConnected => Connection?.State == HubConnectionState.Connected;

    public static Office<IContract> Create()
    {
        var builder = WebApplication.CreateBuilder();

        var office = new Office<IContract>(builder, default);

        builder.Logging.ClearProviders().AddConsole();

        builder.Services.AddRazorPages();
        builder.Services.AddRazorComponents();
        builder.Services.AddServerSideBlazor();
        builder.Services.AddSignalR();

        builder.Services.AddResponseCompression(opts =>
            opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[] { "application/octet-stream" }));
              
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

        if(!App.Environment.IsDevelopment())
        {
            App.UseExceptionHandler("/Error");
            App.UseHsts();
        }

        App.UseRouting();
        App.UseHttpsRedirection();
        App.UseStaticFiles();
        App.MapBlazorHub();

        foreach (var type in Hubs) 
            App.GetType().GetMethod("MapHub", [type])?.Invoke(App, new object[] { SignalR.Address });

        App.RunAsync();  // TODO: consider adding an explicit url

        Connection.StartAsync();

        return this;
    }

    public void Post(Expression<Func<IContract, Delegate>> expression, object? package)
    {
        Task.Run(async () =>
        {
            var methods = typeof(IContract).GetInterfaces().SelectMany(i => i.GetMethods()).ToArray();
            var method = methods.FirstOrDefault(m => expression.ToString().Contains(m.Name));
            if (method is not null && IsConnected)
                await Connection.SendAsync("SendMessage", GetType().Name, method.Name, package);
        });
    }
}
