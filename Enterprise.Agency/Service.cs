using Microsoft.Extensions.DependencyInjection;

namespace Enterprise.Agency;

public static class Helper
{
    public static IServiceCollection AddAgencyServices(this IServiceCollection services, string uri, Func<AgencyCulture, AgencyCulture> opt)
    {
        return services.AddHostedService<Manager>()
                       .AddSingleton(opt(new AgencyCulture(uri)));
    }
}