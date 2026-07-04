namespace Location.Tests;

public sealed class LocationAssemblyTests
{
    [Fact]
    public void Location_project_references_resolve() =>
        Assert.NotNull(typeof(Location.Service.MapPinService).Assembly);
}
