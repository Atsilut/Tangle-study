using Users.Config;
using Users.Infrastructure;

namespace Users.Tests.Security;

public sealed class RedisStartupValidatorTests
{
    [Fact]
    public void Validate_Throws_WhenConnectionStringMissing()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            RedisStartupValidator.Validate(new RedisOptions { ConnectionString = "" }));

        Assert.Contains("ConnectionString", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Throws_WhenConnectionStringWhitespace()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            RedisStartupValidator.Validate(new RedisOptions { ConnectionString = "   " }));

        Assert.Contains("ConnectionString", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_AllowsConfiguredConnectionString()
    {
        var ex = Record.Exception(() =>
            RedisStartupValidator.Validate(new RedisOptions { ConnectionString = "localhost:6379" }));

        Assert.Null(ex);
    }
}
