using Microsoft.Extensions.Hosting;

namespace Api.Global.Security;

public static class JwtStartupValidator
{
    public const string PlaceholderSecret = "CHANGE_THIS_TO_A_LONG_RANDOM_SECRET_KEY_32+_CHARS";

    public static void Validate(IHostEnvironment environment, JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Secret))
            throw new InvalidOperationException("Jwt:Secret is not configured.");

        if (!IsPlaceholderSecret(options.Secret))
            return;

        if (environment.IsDevelopment() || environment.IsEnvironment("Docker"))
            return;

        throw new InvalidOperationException(
            "Jwt:Secret is still the default placeholder in security.yml. Replace Secret before running outside Development/Docker.");
    }

    public static bool IsPlaceholderSecret(string secret) =>
        string.Equals(secret.Trim(), PlaceholderSecret, StringComparison.Ordinal);
}
