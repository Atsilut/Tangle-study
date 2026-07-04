using Microsoft.Extensions.Hosting;

namespace Posts.Security;

/// <summary>
/// Startup guard for JWT configuration. The signing secret is always read from
/// <c>security.yml</c> (<c>Jwt:Secret</c>); production CD injects <c>Jwt__Secret</c> to override the file.
/// </summary>
public static class JwtStartupValidator
{
    /// <summary>
    /// Prefix of the dev placeholder in <c>services/Chat/security.yml</c> — not a signing key.
    /// </summary>
    private const string DevelopmentPlaceholderPrefix = "CHANGE_THIS_TO_A_LONG_RANDOM_SECRET";

    public static void Validate(IHostEnvironment environment, JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Secret))
            throw new InvalidOperationException("Jwt:Secret is not configured in security.yml or Jwt__Secret.");

        if (!IsDevelopmentPlaceholder(options.Secret))
            return;

        if (environment.IsDevelopment() || environment.IsEnvironment("Docker"))
            return;

        throw new InvalidOperationException(
            "Jwt:Secret is still the development placeholder from security.yml. "
            + "Inject Jwt__Secret via CD (same value as the monolith API) before running outside Development/Docker.");
    }

    private static bool IsDevelopmentPlaceholder(string secret) =>
        secret.Trim().StartsWith(DevelopmentPlaceholderPrefix, StringComparison.Ordinal);
}
