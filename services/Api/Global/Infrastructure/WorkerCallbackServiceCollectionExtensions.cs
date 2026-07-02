using Api.Global.Config;
using Api.Global.Security;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Global.Infrastructure;

public static class WorkerCallbackServiceCollectionExtensions
{
    public static IServiceCollection AddTangleWorkerCallbackAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<WorkerCallbackOptions>(configuration.GetSection(WorkerCallbackOptions.SectionName));
        services.AddScoped<WorkerCallbackAuthorizationFilter>();
        return services;
    }
}
