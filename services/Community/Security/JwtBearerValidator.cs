using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Community.Security;

/// <summary>
/// Builds JWT bearer validation parameters. Posts does not issue tokens — login stays on the monolith
/// (future users-service). Interim: each service validates bearer tokens until a gateway owns that step.
/// </summary>
public sealed class JwtBearerValidator(IOptions<JwtOptions> options)
{
    private readonly JwtOptions _options = options.Value;

    public TokenValidationParameters GetValidationParameters()
    {
        var secretKey = string.IsNullOrWhiteSpace(_options.Secret)
            ? throw new InvalidOperationException("Jwt:Secret is not configured.")
            : _options.Secret;

        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,
            ValidateAudience = true,
            ValidAudience = _options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2),
        };
    }
}
