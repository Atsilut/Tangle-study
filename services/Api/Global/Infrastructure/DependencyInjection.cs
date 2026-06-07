using Api.Global.Infrastructure;
using System.Reflection;

public static class DependencyInjection
{
    private static readonly List<(LayerType Layer, string Message)> _logs = [];
    public enum LayerType
    {
        Service,
        Repository
    }

    public static IServiceCollection AddCustomDependencies(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        var types = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract);

        foreach (var type in types)
        {
            var serviceAttr = type.GetCustomAttribute<ServiceAttribute>();
            if (serviceAttr != null)
            {
                Register(services, type, serviceAttr.Lifetime, LayerType.Service);
                continue;
            }

            var repoAttr = type.GetCustomAttribute<RepositoryAttribute>();
            if (repoAttr != null)
                Register(services, type, repoAttr.Lifetime, LayerType.Repository);
        }

        services.AddTransient(typeof(Lazy<>), typeof(LazyService<>));

        return services;
    }

    private static void Register(
    IServiceCollection services,
    Type implementation,
    ServiceLifetime lifetime,
    LayerType layer)
    {
        var interfaces = implementation.GetInterfaces();

        if (interfaces.Length > 0)
        {
            foreach (var i in interfaces)
            {
                var msg = $"[{layer}] {lifetime} {i.FullName} -> {implementation.FullName}";
                _logs.Add((layer, msg));

                services.Add(new ServiceDescriptor(i, implementation, lifetime));
            }
        }
        else
        {
            var msg = $"[{layer}] {lifetime} {implementation.FullName}";
            _logs.Add((layer, msg));

            services.Add(new ServiceDescriptor(implementation, implementation, lifetime));
        }
    }

    public static void PrintLogs(ILogger logger, LayerType? filter = null)
    {
        var logs = filter == null
            ? _logs
            : _logs.Where(x => x.Layer == filter);

        foreach (var group in logs.GroupBy(x => x.Layer))
        {
            logger.LogInformation($"===== {group.Key} Layer =====");

            foreach (var entry in group)
                logger.LogInformation(entry.Message);
        }
    }
}