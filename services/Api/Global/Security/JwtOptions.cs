namespace Api.Global.Security
{
    // Options bound to configuration section "Jwt"
    public class JwtOptions
    {
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        public string Secret { get; set; } = string.Empty;
        public int ExpiryMinutes { get; set; } = 60;
    }
}
