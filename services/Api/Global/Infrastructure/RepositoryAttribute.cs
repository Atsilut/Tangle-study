namespace Api.Global.Infrastructure
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class RepositoryAttribute : Attribute
    {
        public ServiceLifetime Lifetime { get; }

        public RepositoryAttribute(ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            Lifetime = lifetime;
        }
    }
}
