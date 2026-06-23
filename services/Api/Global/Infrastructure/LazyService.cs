namespace Api.Global.Infrastructure;

internal sealed class LazyService<T>(IServiceProvider serviceProvider)
    : Lazy<T>(() => serviceProvider.GetRequiredService<T>()) where T : class;
