using System.Net;
using System.Net.Http.Json;
using Api.Domain.Media.Domain;
using Api.Domain.Media.Dto;
using Api.Domain.Media.Storage;
using Api.Global.Config;
using Api.Global.Queue;
using Api.Global.Security;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Infrastructure;

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
        long storedSizeBytes)
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
        message.Headers.Add(WorkerCallbackAuthorizationFilter.HeaderName, ApiWebApplicationFactory.TestWorkerCallbackSecret);
        var res = await client.SendAsync(message, TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NoContent);
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
        ChatRealtimeTestHelpers.GetWorkQueueStreamKey(options, WorkQueueStreams.MediaUploaded);
}
