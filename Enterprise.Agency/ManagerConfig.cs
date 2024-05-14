using Microsoft.Extensions.DependencyInjection;

namespace Enterprise.Agency;

public static class Helper
{
    public static IServiceCollection AddAgencyServices(this IServiceCollection services, string uri, Func<AgencyCulture, AgencyCulture> builder)
    {
        return services.AddHostedService<Manager>()
                       .AddSingleton(builder(new AgencyCulture(uri)));
    }
}