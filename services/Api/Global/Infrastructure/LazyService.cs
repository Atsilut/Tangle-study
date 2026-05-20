namespace Api.Global.Infrastructure;

internal sealed class LazyService<T> : Lazy<T> where T : class
{
    public LazyService(IServiceProvider serviceProvider)
        : base(() => serviceProvider.GetRequiredService<T>())
    {
    }
}
