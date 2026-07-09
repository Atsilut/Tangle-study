using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gateway.Tests.Infrastructure;

namespace Gateway.Tests.Controllers;

public sealed class GatewayAuthIntegrationTests : IAsyncLifetime
{
    private DownstreamEchoServer _downstream = null!;
    private GatewayWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [Fact]
    public async Task GetPosts_WithoutToken_ProxiesAnonymously()
    {
        var response = await _client.GetAsync("/api/posts", TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(response, HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("/api/posts", body.GetProperty("path").GetString());
        Assert.Equal(string.Empty, body.GetProperty("userId").GetString());
        Assert.Equal(string.Empty, body.GetProperty("gatewaySecret").GetString());
    }

    [Fact]
    public async Task PostPosts_WithoutToken_Returns401()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/posts",
            new { title = "x" },
            TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostPosts_WithValidBearer_ForwardsIdentityHeaders()
    {
        const long userId = 77;
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/posts")
        {
            Content = JsonContent.Create(new { title = "hello" }),
        };
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestJwtFactory.CreateToken(userId));

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(response, HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(userId.ToString(), body.GetProperty("userId").GetString());
        Assert.Equal(TestWebHostConfiguration.GatewaySecret, body.GetProperty("gatewaySecret").GetString());
    }

    [Fact]
    public async Task GetChatRooms_WithAccessTokenQuery_ForwardsIdentityHeaders()
    {
        const long userId = 55;
        var token = TestJwtFactory.CreateToken(userId);
        var response = await _client.GetAsync(
            $"/api/chat/rooms?access_token={Uri.EscapeDataString(token)}",
            TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(response, HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(userId.ToString(), body.GetProperty("userId").GetString());
    }

    public async ValueTask InitializeAsync()
    {
        _downstream = await DownstreamEchoServer.StartAsync();
        _factory = new GatewayWebApplicationFactory(downstreamAddress: _downstream.BaseAddress);
        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        await _downstream.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
