using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Gateway.Config;
using Gateway.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Gateway.Middleware;

/// <summary>
/// Validates bearer JWT once at the edge and forwards trusted identity headers to downstream services.
/// </summary>
public sealed class GatewayAuthMiddleware(
    RequestDelegate next,
    IOptions<GatewayOptions> gatewayOptions,
    JwtBearerValidator jwtValidator)
{
    private static readonly HashSet<string> AnonymousExactPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/login",
        "/api/join",
        "/api/join/nickname-available",
        "/health",
        "/metrics",
    };

    private static readonly string[] AnonymousPrefixes =
    [
    ];

    private static readonly System.Text.RegularExpressions.Regex PublicMediaContentPath =
        new("^/api/media/\\d+/content$", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static readonly System.Text.RegularExpressions.Regex PublicCommunityReadPath =
        new("^/api/(posts|comments)(/|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private readonly RequestDelegate _next = next;
    private readonly GatewayOptions _gatewayOptions = gatewayOptions.Value;
    private readonly TokenValidationParameters _validationParameters = jwtValidator.GetValidationParameters();

    public async Task InvokeAsync(HttpContext context)
    {
        context.Request.Headers.Remove("X-User-Id");
        context.Request.Headers.Remove("X-Gateway-Secret");

        if (IsAnonymous(context.Request))
        {
            await _next(context);
            return;
        }

        var token = ExtractBearerToken(context.Request);
        if (string.IsNullOrWhiteSpace(token))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        try
        {
            var principal = new JwtSecurityTokenHandler().ValidateToken(token, _validationParameters, out _);
            var userId = principal.FindFirst("sub")?.Value
                ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            context.Request.Headers["X-User-Id"] = userId;
            if (!string.IsNullOrWhiteSpace(_gatewayOptions.Secret))
                context.Request.Headers["X-Gateway-Secret"] = _gatewayOptions.Secret;
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
        {
            // Malformed tokens throw SecurityTokenMalformedException (ArgumentException), not SecurityTokenException.
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await _next(context);
    }

    private static bool IsAnonymous(HttpRequest request)
    {
        var path = request.Path.Value ?? string.Empty;
        if (AnonymousExactPaths.Contains(path)) return true;
        if (AnonymousPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))) return true;

        if (request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase)
            && (path.Equals("/api/users", StringComparison.OrdinalIgnoreCase)
                || System.Text.RegularExpressions.Regex.IsMatch(path, "^/api/users/\\d+$")
                || PublicMediaContentPath.IsMatch(path)
                || PublicCommunityReadPath.IsMatch(path)))
            return true;

        return false;
    }

    private static string? ExtractBearerToken(HttpRequest request)
    {
        var authorization = request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization)
            && authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return authorization["Bearer ".Length..].Trim();

        if (request.Query.TryGetValue("access_token", out var accessToken)
            && !string.IsNullOrWhiteSpace(accessToken))
            return accessToken.ToString();

        return null;
    }
}
