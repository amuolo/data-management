using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Agency;

public class Navigator : NavigationManager 
{
    public Navigator(string baseUri, string uri) => Initialize(baseUri, uri);
}

public static class Configurator
{
    public static WebApplicationBuilder Create()
    {
        var builder = WebApplication.CreateBuilder();

        builder.Services.AddSignalR();

        return builder;
    }
}
