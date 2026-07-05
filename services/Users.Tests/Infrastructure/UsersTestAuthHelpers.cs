namespace Users.Tests.Infrastructure;

internal static class UsersTestAuthHelpers
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

    public static void LoginAsInternal(HttpClient client)
    {
        client.DefaultRequestHeaders.Remove("X-Gateway-Secret");
        client.DefaultRequestHeaders.Remove("X-User-Id");
        client.DefaultRequestHeaders.Remove("Authorization");
        client.DefaultRequestHeaders.Add(
            "X-Internal-Secret",
            UsersWebApplicationFactory.TestInternalServiceSecret);
    }

    public static void ClearAuth(HttpClient client)
    {
        client.DefaultRequestHeaders.Authorization = null;
        client.DefaultRequestHeaders.Remove("X-Internal-Secret");
        client.DefaultRequestHeaders.Remove("X-Gateway-Secret");
        client.DefaultRequestHeaders.Remove("X-User-Id");
    }
}
