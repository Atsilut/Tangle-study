using System.Net.Http.Json;
using Api.Domain.Location.Dto;
using Api.Global.Security;

namespace Api.Tests.Infrastructure;

internal static class LocationIntegrationTestHelpers
{
    public const string SessionsBase = "/api/location/sessions";
    public const string InternalLocationBase = "/internal/location";

    public static Task<HttpResponseMessage> SendWorkerRequestAsync(
        HttpClient client,
        HttpMethod method,
        string url,
        object? body = null,
        string? workerSecret = ApiWebApplicationFactory.TestWorkerCallbackSecret)
    {
        var message = new HttpRequestMessage(method, url);
        if (body is not null)
            message.Content = JsonContent.Create(body);

        if (workerSecret is not null)
            message.Headers.Add(WorkerCallbackAuthorizationFilter.HeaderName, workerSecret);

        return client.SendAsync(message, TestContext.Current.CancellationToken);
    }

    public static string BuildClusterPointsQuery(MapPinBoundsQueryDto query)
    {
        var format = (decimal value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return
            $"minLatitude={format(query.MinLatitude)}" +
            $"&maxLatitude={format(query.MaxLatitude)}" +
            $"&minLongitude={format(query.MinLongitude)}" +
            $"&maxLongitude={format(query.MaxLongitude)}";
    }
}
