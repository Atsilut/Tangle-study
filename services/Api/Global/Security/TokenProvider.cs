using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Api.Global.Security
{
    public class TokenProvider
    {
        private readonly string _issuer;
        private readonly string _audience;
        private readonly string _secretKey;
        private readonly TimeSpan _tokenExpiryMinutes;

        public TokenProvider(IOptions<JwtOptions> options)
        {
            var jwt = options.Value;

            _issuer = jwt.Issuer;
            _audience = jwt.Audience;
            _secretKey = string.IsNullOrWhiteSpace(jwt.Secret)
                ? throw new InvalidOperationException("Jwt:Secret is not configured.")
                : jwt.Secret;
            _tokenExpiryMinutes = TimeSpan.FromMinutes(jwt.ExpiryMinutes);
        }

        public string GenerateToken(long userId)
        {
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(ClaimTypes.NameIdentifier, userId.ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _issuer,
                audience: _audience,
                claims: claims,
                expires: DateTime.UtcNow.Add(_tokenExpiryMinutes),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public TokenValidationParameters GetValidationParameters()
        {
            return new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = _audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2)
            };
        }
    }
}