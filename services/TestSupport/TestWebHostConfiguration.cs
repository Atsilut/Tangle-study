namespace Tangle.TestSupport;

public static class TestWebHostConfiguration
{
    public const string GatewaySecret = "test-gateway-secret";
    public const string InternalServiceSecret = "test-internal-service-secret";
    public const string JwtSecret = "integration-test-jwt-secret-at-least-32-characters-long";
    public const string JwtIssuer = "Tangle";
    public const string JwtAudience = "TangleClient";
}
