using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Community.Tests.Infrastructure;

internal static class CommunityTestAuthHelpers
{
    public static void LoginAs(HttpClient client, long userId, string? secret = null)
    {
        secret ??= CommunityWebApplicationFactory.TestJwtSecret;
        var token = CreateToken(userId, secret);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public static void LoginAsInternal(HttpClient client, long? userId = null)
    {
        client.DefaultRequestHeaders.Remove("X-Internal-Secret");
        client.DefaultRequestHeaders.Add(
            "X-Internal-Secret",
            CommunityWebApplicationFactory.TestInternalServiceSecret);
        if (userId is not null) LoginAs(client, userId.Value);
    }

    public static string CreateToken(long userId, string? secret = null)
    {
        secret ??= CommunityWebApplicationFactory.TestJwtSecret;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: CommunityWebApplicationFactory.TestJwtIssuer,
            audience: CommunityWebApplicationFactory.TestJwtAudience,
            claims: [new Claim("sub", userId.ToString())],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
