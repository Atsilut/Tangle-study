using Microsoft.Extensions.Hosting;

namespace Gateway.Security;

/// <summary>
/// Startup guard for JWT configuration at the gateway edge.
/// </summary>
public static class JwtStartupValidator
{
    public const string PlaceholderSecret = "CHANGE_THIS_TO_A_LONG_RANDOM_SECRET_KEY_32+_CHARS";

    public static void Validate(IHostEnvironment environment, JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Secret))
            throw new InvalidOperationException("Jwt:Secret is not configured in security.yml or Jwt__Secret.");

        if (!IsPlaceholderSecret(options.Secret))
            return;

        if (environment.IsDevelopment() || environment.IsEnvironment("Docker"))
            return;

        throw new InvalidOperationException(
            "Jwt:Secret is still the development placeholder from security.yml. "
            + "Inject Jwt__Secret via CD before running outside Development/Docker.");
    }

    public static bool IsPlaceholderSecret(string secret) =>
        string.Equals(secret.Trim(), PlaceholderSecret, StringComparison.Ordinal);
}
