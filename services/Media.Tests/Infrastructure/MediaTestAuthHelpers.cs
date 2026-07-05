namespace Media.Tests.Infrastructure;

internal static class MediaTestAuthHelpers
{
    public const string TestGatewaySecret = "test-gateway-secret";

    public static void LoginAs(HttpClient client, long userId)
    {
        client.DefaultRequestHeaders.Remove("Authorization");
        client.DefaultRequestHeaders.Remove("X-User-Id");
        client.DefaultRequestHeaders.Remove("X-Gateway-Secret");
        client.DefaultRequestHeaders.Add("X-Gateway-Secret", TestGatewaySecret);
        client.DefaultRequestHeaders.Add("X-User-Id", userId.ToString());
    }
}
