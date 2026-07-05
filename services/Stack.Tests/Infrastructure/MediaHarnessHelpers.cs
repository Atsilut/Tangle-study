using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Media;
using Media.Dto;

namespace Stack.Tests.Infrastructure;

internal static class MediaHarnessHelpers
{
    public const string ApiBaseUrlEnv = "TANGLE_HARNESS_API_BASE_URL";
    public const long PostImagePerFileBytes = 157_286_400;
    public const long PostVideoPerFileBytes = 2_147_483_648;

    public static HttpClient CreateHarnessClient()
    {
        var baseUrl = Environment.GetEnvironmentVariable(ApiBaseUrlEnv)
            ?? throw new InvalidOperationException($"{ApiBaseUrlEnv} is not set.");
        return new HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromMinutes(3),
        };
    }

    public static async Task WaitForApiReadyAsync(HttpClient client, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        Exception? lastError = null;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = await client.GetAsync("health", TestContext.Current.CancellationToken);
                if (response.IsSuccessStatusCode) return;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        }

        throw new TimeoutException(
            $"API at {client.BaseAddress} did not become ready within {timeout}.",
            lastError);
    }

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
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            var response = await client.GetAsync($"api/media/{mediaAssetId}", TestContext.Current.CancellationToken);
            await IntegrationAssertions.AssertStatusAsync(response, HttpStatusCode.OK);
            var asset = await response.Content.ReadFromJsonAsync<MediaAssetGetResponseDto>(
                TestContext.Current.CancellationToken);
            Assert.NotNull(asset);

            if (asset.ProcessingStatus == MediaProcessingStatus.Ready) return asset;
            if (asset.ProcessingStatus == MediaProcessingStatus.Failed)
            {
                throw new InvalidOperationException(
                    $"Media asset {mediaAssetId} failed processing: {asset.FailureReason ?? "(no reason)"}");
            }

            await Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        }

        throw new TimeoutException($"Media asset {mediaAssetId} did not reach Ready within {timeout}.");
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
