using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Chat.Tests.Infrastructure;

internal static class ChatTestAuthHelpers
{
    public static void LoginAs(HttpClient client, long userId, string? secret = null)
    {
        secret ??= ChatWebApplicationFactory.TestJwtSecret;
        var token = CreateToken(userId, secret);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public static string CreateToken(long userId, string? secret = null)
    {
        secret ??= ChatWebApplicationFactory.TestJwtSecret;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: ChatWebApplicationFactory.TestJwtIssuer,
            audience: ChatWebApplicationFactory.TestJwtAudience,
            claims: [new Claim("sub", userId.ToString())],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    internal static string BuildNickname(string testMethodName, long index) =>
        $"{testMethodName}User{index}";
}
