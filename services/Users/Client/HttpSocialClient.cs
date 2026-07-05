using System.Net;
using System.Text.Json;
using Users.Config;
using Users.Exceptions;
using Microsoft.Extensions.Options;

namespace Users.Client;

internal sealed class HttpSocialClient(
    IHttpClientFactory httpClientFactory,
    IOptions<SocialClientOptions> options) : ISocialClient
{
    public const string InternalSecretHeaderName = "X-Internal-Secret";

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly SocialClientOptions _options = options.Value;

    public Task DetachUserOnDeletionAsync(long userId, CancellationToken cancellationToken = default) =>
        PostNoContentAsync($"internal/social/users/{userId}/detach-on-deletion", cancellationToken);

    private async Task PostNoContentAsync(string relativePath, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(nameof(HttpSocialClient));
        using var request = new HttpRequestMessage(HttpMethod.Post, relativePath);

        if (!string.IsNullOrWhiteSpace(_options.InternalSecret))
            request.Headers.TryAddWithoutValidation(InternalSecretHeaderName, _options.InternalSecret);

        using var response = await client.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode) return;
        await ThrowForFailureAsync(response, cancellationToken);
    }

    private static async Task ThrowForFailureAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var detail = await ReadProblemDetailAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new ArgumentException(detail);

        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new EntityNotFoundException(detail);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new UnauthorizedAccessException(detail);

        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new EntityAlreadyExistsException(detail);

        throw new InvalidOperationException(
            $"Social detach failed ({(int)response.StatusCode}): {detail}");
    }

    private static async Task<string> ReadProblemDetailAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(body))
            return response.ReasonPhrase ?? "Social request failed";

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("detail", out var detailElement)
                && detailElement.ValueKind == JsonValueKind.String)
                return detailElement.GetString() ?? body;
        }
        catch (JsonException)
        {
        }

        return body;
    }
}
