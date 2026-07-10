using System.Net;
using System.Net.Http.Json;
using Media.Storage;
using Media.Dto;
using Media.Config;
using Media.Queue;
using Media.Security;
using Microsoft.Extensions.DependencyInjection;
using Media.Entities;
using Tangle.AspNetCore.Security;

namespace Media.Tests.Infrastructure;

internal static class MediaIntegrationTestHelpers
{
    public const long PostVideoPerFileBytes = 2_147_483_648;
    public const int IngressMultiplier = 3;

    public static async Task<MediaUploadInitResponseDto> InitUploadAsync(
        HttpClient client,
        MediaIntendedContext context,
        string mimeType,
        string fileName,
        long sizeBytes)
    {
        var req = new MediaUploadInitRequestDto
        {
            IntendedContext = context,
            MimeType = mimeType,
            FileName = fileName,
            SizeBytes = sizeBytes,
        };
        var res = await client.PostAsJsonAsync("/api/media/upload-init", req, TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<MediaUploadInitResponseDto>(TestContext.Current.CancellationToken))!;
    }

    public static async Task<MediaAssetGetResponseDto> CompleteUploadAsync(HttpClient client, long mediaAssetId)
    {
        var res = await client.PostAsync($"/api/media/{mediaAssetId}/complete", null, TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<MediaAssetGetResponseDto>(TestContext.Current.CancellationToken))!;
    }

    public static async Task MarkProcessedReadyAsync(
        HttpClient client,
        long mediaAssetId,
        string processedObjectKey,
        long storedSizeBytes,
        string? workerSecret = IntegrationTestConstants.TestWorkerCallbackSecret)
    {
        var req = new MediaProcessedRequestDto
        {
            ProcessedObjectKey = processedObjectKey,
            StoredSizeBytes = storedSizeBytes,
        };
        using var message = new HttpRequestMessage(HttpMethod.Patch, $"/internal/media/{mediaAssetId}/processed")
        {
            Content = JsonContent.Create(req),
        };
        if (workerSecret is not null)
            message.Headers.Add(WorkerCallbackAuthorizationFilter.HeaderName, workerSecret);
        var res = await client.SendAsync(message, TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NoContent);
    }

    public static Task<HttpResponseMessage> SendProcessedCallbackAsync(
        HttpClient client,
        long mediaAssetId,
        MediaProcessedRequestDto request,
        string? workerSecret = IntegrationTestConstants.TestWorkerCallbackSecret)
    {
        var message = new HttpRequestMessage(HttpMethod.Patch, $"/internal/media/{mediaAssetId}/processed")
        {
            Content = JsonContent.Create(request),
        };
        if (workerSecret is not null)
            message.Headers.Add(WorkerCallbackAuthorizationFilter.HeaderName, workerSecret);
        return client.SendAsync(message, TestContext.Current.CancellationToken);
    }

    public static async Task<long> UploadAndMarkReadyAsync(
        HttpClient client,
        MediaIntendedContext context,
        string mimeType,
        string fileName,
        long declaredSizeBytes,
        long storedSizeBytes)
    {
        var init = await InitUploadAsync(client, context, mimeType, fileName, declaredSizeBytes);
        var completed = await CompleteUploadAsync(client, init.MediaAssetId);
        Assert.Equal(MediaProcessingStatus.Processing, completed.ProcessingStatus);
        await MarkProcessedReadyAsync(
            client,
            init.MediaAssetId,
            $"processed/{init.MediaAssetId}/{fileName}",
            storedSizeBytes);
        return init.MediaAssetId;
    }

    public static FakeMediaStorage GetFakeStorage(IServiceProvider services) =>
        (FakeMediaStorage)services.GetRequiredService<IMediaStorage>();

    public static string GetMediaUploadedStreamKey(RedisOptions options) =>
        $"{options.WorkQueueStreamPrefix}{WorkQueueStreams.MediaUploaded}";

    public static async Task LinkToPostAsync(
        HttpClient client,
        long postId,
        long uploaderUserId,
        IReadOnlyList<long> mediaAssetIds)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/internal/media/link/post")
        {
            Content = JsonContent.Create(new LinkPostMediaRequestDto(postId, uploaderUserId, mediaAssetIds)),
        };
        message.Headers.Add(InternalAccessAuthorizationFilter.HeaderName, GatewayTestAuthHelpers.TestInternalServiceSecret);
        var res = await client.SendAsync(message, TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NoContent);
    }
}
