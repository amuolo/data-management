using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace Agency;

public class Office<IContract>(WebApplicationBuilder Builder, WebApplication? App) 
    where IContract : class
{
    private List<Type> Hubs { get; } = [];

    private MessageHub<IContract> MessageHub { get; set; }

    public static Office<IContract> Create()
    {
        var builder = WebApplication.CreateBuilder();

        var office = new Office<IContract>(builder, default) { MessageHub = new MessageHub<IContract>() };

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
            App.GetType().GetMethod("MapHub", [type])?.Invoke(App, new object[] { Contract.Address });

        App.RunAsync();  // TODO: consider adding an explicit url

        return this;
    }

    public void Post(Expression<Func<IContract, Delegate>> expression, object? package)
        => MessageHub.Post(expression, package);
}
