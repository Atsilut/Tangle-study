using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Media.Dto;
using Media.Entities;
using Tangle.TestSupport.Integration;

namespace Stack.Tests.Infrastructure;

internal static class MediaHarnessHelpers
{
    public const long PostImagePerFileBytes = 157_286_400;
    public const long PostVideoPerFileBytes = 2_147_483_648;

    public static async Task UploadFixtureToBlobAsync(string uploadUrl, byte[] bytes, string contentType)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        using var request = new HttpRequestMessage(HttpMethod.Put, uploadUrl)
        {
            Content = content,
        };
        request.Headers.Add("x-ms-blob-type", "BlockBlob");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        if (response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        throw new InvalidOperationException(
            $"Blob upload failed with {(int)response.StatusCode} {response.StatusCode}. Body: {body}");
    }

    public static async Task<MediaAssetGetResponseDto> PollUntilTerminalAsync(
        HttpClient client,
        long mediaAssetId,
        TimeSpan timeout)
    {
        var asset = await IntegrationTestPolling.PollUntilAsync(
            async ct =>
            {
                var response = await client.GetAsync($"api/media/{mediaAssetId}", ct);
                await IntegrationAssertions.AssertStatusAsync(response, HttpStatusCode.OK);
                return (await response.Content.ReadFromJsonAsync<MediaAssetGetResponseDto>(ct))!;
            },
            a => a.ProcessingStatus is MediaProcessingStatus.Ready or MediaProcessingStatus.Failed,
            timeout,
            delay: TimeSpan.FromSeconds(1),
            cancellationToken: TestContext.Current.CancellationToken);

        if (asset.ProcessingStatus == MediaProcessingStatus.Failed)
        {
            throw new InvalidOperationException(
                $"Media asset {mediaAssetId} failed processing: {asset.FailureReason ?? "(no reason)"}");
        }

        return asset;
    }

    public static string GetFixturePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Harness", fileName);

    public static async Task<byte[]> ReadFixtureAsync(string fileName)
    {
        var path = GetFixturePath(fileName);
        Assert.True(File.Exists(path), $"Harness fixture not found: {path}");
        return await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
    }

    private static async Task<MediaUploadInitResponseDto> InitUploadAsync(
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
        var res = await client.PostAsJsonAsync("api/media/upload-init", req, TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<MediaUploadInitResponseDto>(TestContext.Current.CancellationToken))!;
    }

    private static async Task<MediaAssetGetResponseDto> CompleteUploadAsync(HttpClient client, long mediaAssetId)
    {
        var res = await client.PostAsync($"api/media/{mediaAssetId}/complete", null, TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<MediaAssetGetResponseDto>(TestContext.Current.CancellationToken))!;
    }

    public static async Task<MediaAssetGetResponseDto> UploadFixtureThroughPipelineAsync(
        HttpClient client,
        string fixtureFileName,
        string mimeType,
        MediaIntendedContext context,
        TimeSpan processingTimeout)
    {
        var bytes = await ReadFixtureAsync(fixtureFileName);
        var init = await InitUploadAsync(client, context, mimeType, fixtureFileName, bytes.Length);
        await UploadFixtureToBlobAsync(init.UploadUrl, bytes, mimeType);

        var completed = await CompleteUploadAsync(client, init.MediaAssetId);
        Assert.Equal(MediaProcessingStatus.Processing, completed.ProcessingStatus);

        return await PollUntilTerminalAsync(client, init.MediaAssetId, processingTimeout);
    }
}
