using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Media.Tests.Infrastructure;

internal static class MediaTestAuthHelpers
{
    public static void LoginAs(HttpClient client, long userId, string? secret = null)
    {
        secret ??= MediaWebApplicationFactory.TestJwtSecret;
        var token = CreateToken(userId, secret);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static string CreateToken(long userId, string secret)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: MediaWebApplicationFactory.TestJwtIssuer,
            audience: MediaWebApplicationFactory.TestJwtAudience,
            claims: [new Claim("sub", userId.ToString())],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
