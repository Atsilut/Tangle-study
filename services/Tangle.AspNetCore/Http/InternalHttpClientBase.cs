using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tangle.AspNetCore.Config;
using Tangle.AspNetCore.Exceptions;
using Tangle.AspNetCore.Security;

namespace Tangle.AspNetCore.Http;

public abstract class InternalHttpClientBase(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    InternalServiceClientOptions options,
    string httpClientName)
{
    protected static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    protected readonly IHttpClientFactory HttpClientFactory = httpClientFactory;
    protected readonly IHttpContextAccessor HttpContextAccessor = httpContextAccessor;
    protected readonly InternalServiceClientOptions Options = options;
    protected readonly string HttpClientName = httpClientName;

    protected virtual bool RemapAuthenticatedUnauthorizedToForbidden => false;

    protected Task PostNoContentAsync(string relativePath, CancellationToken cancellationToken = default) =>
        PostNoContentAsync(relativePath, content: null, cancellationToken);

    protected async Task PostNoContentAsync(
        string relativePath,
        object? content,
        CancellationToken cancellationToken = default)
    {
        using var response = await PostAsync(relativePath, content, cancellationToken);
        if (response.IsSuccessStatusCode) return;
        await ThrowForFailureAsync(response, cancellationToken);
    }

    protected async Task GetNoContentAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        using var response = await GetAsync(relativePath, cancellationToken);
        if (response.IsSuccessStatusCode) return;
        await ThrowForFailureAsync(response, cancellationToken);
    }

    protected async Task<T?> PostJsonAsync<T>(
        string relativePath,
        object? content,
        CancellationToken cancellationToken = default)
    {
        using var response = await PostAsync(relativePath, content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            await ThrowForFailureAsync(response, cancellationToken);
            return default;
        }

        return await response.Content.ReadFromJsonAsync<T>(SerializerOptions, cancellationToken);
    }

    protected async Task DeleteNoContentAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        using var response = await DeleteAsync(relativePath, cancellationToken);
        if (response.IsSuccessStatusCode) return;
        await ThrowForFailureAsync(response, cancellationToken);
    }

    protected async Task DeleteNoContentAsync(
        string relativePath,
        object content,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Delete, relativePath, content, cancellationToken);
        if (response.IsSuccessStatusCode) return;
        await ThrowForFailureAsync(response, cancellationToken);
    }

    protected Task<HttpResponseMessage> GetAsync(string relativePath, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Get, relativePath, content: null, cancellationToken);

    protected Task<HttpResponseMessage> PostAsync(
        string relativePath,
        object? content,
        CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, relativePath, content, cancellationToken);

    protected Task<HttpResponseMessage> PutAsync(
        string relativePath,
        object? content,
        CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Put, relativePath, content, cancellationToken);

    protected Task<HttpResponseMessage> DeleteAsync(string relativePath, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Delete, relativePath, content: null, cancellationToken);

    protected async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string relativePath,
        object? content,
        CancellationToken cancellationToken)
    {
        var client = HttpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(method, relativePath);

        if (content is not null)
            request.Content = JsonContent.Create(content, options: SerializerOptions);

        var incoming = HttpContextAccessor.HttpContext?.Request.Headers;
        var authorization = incoming?.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization))
            request.Headers.TryAddWithoutValidation("Authorization", authorization);

        var userId = incoming?[GatewayIdentityAuthenticationHandler.UserIdHeaderName].ToString();
        if (!string.IsNullOrWhiteSpace(userId))
            request.Headers.TryAddWithoutValidation(
                GatewayIdentityAuthenticationHandler.UserIdHeaderName,
                userId);

        var gatewaySecret = incoming?[GatewayIdentityAuthenticationHandler.GatewaySecretHeaderName].ToString();
        if (!string.IsNullOrWhiteSpace(gatewaySecret))
            request.Headers.TryAddWithoutValidation(
                GatewayIdentityAuthenticationHandler.GatewaySecretHeaderName,
                gatewaySecret);

        if (!string.IsNullOrWhiteSpace(Options.InternalSecret))
            request.Headers.TryAddWithoutValidation("X-Internal-Secret", Options.InternalSecret);

        return await client.SendAsync(request, cancellationToken);
    }

    protected virtual async Task ThrowForFailureAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var detail = await ReadProblemDetailAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new ArgumentException(detail);

        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new EntityNotFoundException(detail);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            if (RemapAuthenticatedUnauthorizedToForbidden
                && HttpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true)
                throw new AccessForbiddenException(detail);
            throw new UnauthorizedAccessException(detail);
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
            throw new AccessForbiddenException(detail);

        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new EntityAlreadyExistsException(detail);

        throw new InvalidOperationException(
            $"Internal service call failed ({(int)response.StatusCode}): {detail}");
    }

    protected static async Task<string> ReadProblemDetailAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(body))
            return response.ReasonPhrase ?? "Access denied";

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
