using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Tangle.AspNetCore.Security;

namespace Users.Tests.Security;

public sealed class JwtStartupValidatorTests
{
    [Fact]
    public void Validate_AllowsPlaceholderSecret_InDevelopment()
    {
        var environment = new FakeHostEnvironment(Environments.Development);

        var ex = Record.Exception(() =>
            JwtStartupValidator.Validate(environment, JwtStartupValidator.PlaceholderSecret));

        Assert.Null(ex);
    }

    [Fact]
    public void Validate_AllowsPlaceholderSecret_InDocker()
    {
        var environment = new FakeHostEnvironment("Docker");

        var ex = Record.Exception(() =>
            JwtStartupValidator.Validate(environment, JwtStartupValidator.PlaceholderSecret));

        Assert.Null(ex);
    }

    [Fact]
    public void Validate_ThrowsInProduction_WhenPlaceholderSecret()
    {
        var environment = new FakeHostEnvironment(Environments.Production);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            JwtStartupValidator.Validate(environment, JwtStartupValidator.PlaceholderSecret));

        Assert.Contains("placeholder", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_ThrowsInProduction_WhenSecretMissing()
    {
        var environment = new FakeHostEnvironment(Environments.Production);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            JwtStartupValidator.Validate(environment, "   "));

        Assert.Contains("not configured", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_AllowsCustomSecret_InProduction()
    {
        var environment = new FakeHostEnvironment(Environments.Production);

        var ex = Record.Exception(() =>
            JwtStartupValidator.Validate(
                environment,
                "production-jwt-secret-at-least-32-characters-long"));

        Assert.Null(ex);
    }

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Users.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
