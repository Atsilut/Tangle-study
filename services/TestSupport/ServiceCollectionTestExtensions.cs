using Microsoft.Extensions.DependencyInjection;

namespace Tangle.TestSupport;

public static class ServiceCollectionTestExtensions
{
    public static void RemoveService<T>(this IServiceCollection services)
    {
        var descriptors = services.Where(d => d.ServiceType == typeof(T)).ToList();
        foreach (var descriptor in descriptors)
            services.Remove(descriptor);
    }
}
