using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace Agency;

public class Office<IContract>(WebApplicationBuilder Builder, WebApplication? App) : MessageHub<IContract>
    where IContract : class
{
    private List<Type> Hubs { get; } = [];

    private CancellationToken CancellationToken { get; set; } = new();

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
            where THub : MessageHub<IHubContract>, new()
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
            App.GetType().GetMethod("MapHub", [type])?.Invoke(App, new object[] { Contract.SignalRAddress });

        App.RunAsync();  // TODO: consider adding an explicit url

        Task.Run(async () => await InitializeConnectionAsync(CancellationToken));

        return this;
    }

    public Office<IContract> Register<TReceived>(Expression<Func<IContract, Delegate>> predicate, Action<TReceived> action)
    {
        if (!GetMessage(predicate, out var message) || !IsAlive()) return this;
        
        OperationByPredicate.TryAdd(message, (typeof(TReceived), action));

        return this;
    }

    public Office<IContract> Register(Expression<Func<IContract, Delegate>> predicate, Action action)
    {
        if (!GetMessage(predicate, out var message)) return this;

        OperationByPredicate.TryAdd(message, (null, action));

        return this;
    }
}
