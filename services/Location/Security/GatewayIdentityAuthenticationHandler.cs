using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Location.Config;

namespace Location.Security;

public sealed class GatewayIdentityAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<GatewayIdentityOptions> gatewayOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "GatewayIdentity";
    public const string UserIdHeaderName = "X-User-Id";
    public const string GatewaySecretHeaderName = "X-Gateway-Secret";

    private readonly GatewayIdentityOptions _gatewayOptions = gatewayOptions.Value;

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (string.IsNullOrWhiteSpace(_gatewayOptions.Secret))
            return Task.FromResult(AuthenticateResult.NoResult());

        if (!Request.Headers.TryGetValue(GatewaySecretHeaderName, out var secret)
            || secret != _gatewayOptions.Secret)
            return Task.FromResult(AuthenticateResult.NoResult());

        if (!Request.Headers.TryGetValue(UserIdHeaderName, out var userIdHeader)
            || !long.TryParse(userIdHeader, out var userId))
            return Task.FromResult(AuthenticateResult.Fail("Missing gateway user identity."));

        Claim[] claims =
        [
            new Claim("sub", userId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
        ];
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
