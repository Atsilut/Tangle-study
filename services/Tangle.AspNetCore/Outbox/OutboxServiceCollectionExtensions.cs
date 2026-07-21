using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Tangle.AspNetCore.Outbox;

public static class OutboxServiceCollectionExtensions
{
    public static IServiceCollection AddTangleOutbox<TContext>(
        this IServiceCollection services,
        IConfiguration configuration)
        where TContext : DbContext
    {
        services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));
        services.TryAddSingleton<NoOpOutboxEventPublisher>();
        services.AddScoped<IOutboxWriter, EfOutboxWriter<TContext>>();
        services.AddHostedService<OutboxDispatcherHostedService<TContext>>();
        return services;
    }
}
