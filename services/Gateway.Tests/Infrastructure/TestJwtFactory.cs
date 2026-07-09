using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Gateway.Tests.Infrastructure;

public static class TestJwtFactory
{
    public static string CreateToken(long userId, TimeSpan? lifetime = null)
    {
        Claim[] claims =
        [
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
        ];

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestWebHostConfiguration.JwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: TestWebHostConfiguration.JwtIssuer,
            audience: TestWebHostConfiguration.JwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromMinutes(30)),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
