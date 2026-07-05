namespace Community.Tests.Infrastructure;

internal static class CommunityTestAuthHelpers
{
    public const string TestGatewaySecret = "test-gateway-secret";

    public static void LoginAs(HttpClient client, long userId)
    {
        client.DefaultRequestHeaders.Remove("X-Internal-Secret");
        client.DefaultRequestHeaders.Remove("Authorization");
        client.DefaultRequestHeaders.Remove("X-User-Id");
        client.DefaultRequestHeaders.Remove("X-Gateway-Secret");
        client.DefaultRequestHeaders.Add("X-Gateway-Secret", TestGatewaySecret);
        client.DefaultRequestHeaders.Add("X-User-Id", userId.ToString());
    }

    public static void LoginAsInternal(HttpClient client, long? userId = null)
    {
        client.DefaultRequestHeaders.Remove("Authorization");
        client.DefaultRequestHeaders.Remove("X-Gateway-Secret");
        client.DefaultRequestHeaders.Remove("X-User-Id");
        client.DefaultRequestHeaders.Remove("X-Internal-Secret");

        if (userId is not null)
        {
            client.DefaultRequestHeaders.Add("X-Gateway-Secret", TestGatewaySecret);
            client.DefaultRequestHeaders.Add("X-User-Id", userId.Value.ToString());
        }

        client.DefaultRequestHeaders.Add(
            "X-Internal-Secret",
            CommunityWebApplicationFactory.TestInternalServiceSecret);
    }
}
