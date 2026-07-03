using System.Net;
using System.Net.Http.Json;
using Media;
using Media.Dto;
using Media.Config;
using Media.Queue;
using Media.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Media.Tests.Controllers;

[Collection(MediaRedisIntegrationTestCollection.Name)]
public sealed class MediaIntegrationTests(
    PostgresTestcontainerFixture postgres,
    RedisTestcontainerFixture redis)
    : IntegrationTestBase(postgres, redisEnabled: true, redisConnectionString: redis.ConnectionString)
{
    [Fact]
    public async Task InitUpload_Returns400_WhenDeclaredSizeExceedsIngressLimit()
    {
        MediaTestAuthHelpers.LoginAs(Client, userId: 1);
        const long ingressLimit = MediaIntegrationTestHelpers.PostVideoPerFileBytes * MediaIntegrationTestHelpers.IngressMultiplier;
        var req = new MediaUploadInitRequestDto
        {
            IntendedContext = MediaIntendedContext.Post,
            MimeType = "video/mp4",
            FileName = "too-large.mp4",
            SizeBytes = ingressLimit + 1,
        };

        var res = await Client.PostAsJsonAsync("/api/media/upload-init", req, TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.BadRequest);
        await IntegrationAssertions.AssertProblemDetailContainsAsync(res, "upload limit");
    }

    [Fact]
    public async Task CompleteUpload_EnqueuesMediaUploadedJob_ToRedisStream()
    {
        MediaTestAuthHelpers.LoginAs(Client, userId: 1);
        var init = await MediaIntegrationTestHelpers.InitUploadAsync(
            Client,
            MediaIntendedContext.Post,
            "video/mp4",
            "stream-test.mp4",
            sizeBytes: 1_000);
        var (database, streamKey, lengthBefore) = await GetMediaStreamLengthAsync();

        var completed = await MediaIntegrationTestHelpers.CompleteUploadAsync(Client, init.MediaAssetId);

        Assert.Equal(MediaProcessingStatus.Processing, completed.ProcessingStatus);
        var lengthAfter = await database.StreamLengthAsync(streamKey);
        Assert.Equal(lengthBefore + 1, lengthAfter);
        await database.Multiplexer.CloseAsync();
    }

    [Fact]
    public async Task CompleteUpload_Returns400_WhenBlobMissing()
    {
        MediaTestAuthHelpers.LoginAs(Client, userId: 1);
        var init = await MediaIntegrationTestHelpers.InitUploadAsync(
            Client,
            MediaIntendedContext.Post,
            "video/mp4",
            "missing-blob.mp4",
            sizeBytes: 1_000);
        await using var scope = Factory.Services.CreateAsyncScope();
        MediaIntegrationTestHelpers.GetFakeStorage(scope.ServiceProvider).RemoveObject(init.ObjectKey);

        var res = await Client.PostAsync($"/api/media/{init.MediaAssetId}/complete", null, TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.BadRequest);
        await IntegrationAssertions.AssertProblemDetailContainsAsync(res, "not found in storage");
    }

    [Fact]
    public async Task WorkerCallback_MarksAssetReady()
    {
        MediaTestAuthHelpers.LoginAs(Client, userId: 1);
        var init = await MediaIntegrationTestHelpers.InitUploadAsync(
            Client,
            MediaIntendedContext.Post,
            "image/jpeg",
            "callback.jpg",
            sizeBytes: 50_000);
        await MediaIntegrationTestHelpers.CompleteUploadAsync(Client, init.MediaAssetId);

        await MediaIntegrationTestHelpers.MarkProcessedReadyAsync(
            Client,
            init.MediaAssetId,
            $"processed/{init.MediaAssetId}/callback.jpg",
            storedSizeBytes: 40_000);

        var getRes = await Client.GetAsync($"/api/media/{init.MediaAssetId}", TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(getRes, HttpStatusCode.OK);
        var asset = await getRes.Content.ReadFromJsonAsync<MediaAssetGetResponseDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(asset);
        Assert.Equal(MediaProcessingStatus.Ready, asset.ProcessingStatus);
        Assert.Equal(40_000, asset.StoredSizeBytes);
    }

    [Fact]
    public async Task WorkerCallback_Returns401_WithoutWorkerSecret()
    {
        MediaTestAuthHelpers.LoginAs(Client, userId: 1);
        var init = await MediaIntegrationTestHelpers.InitUploadAsync(
            Client,
            MediaIntendedContext.Post,
            "image/jpeg",
            "no-secret.jpg",
            sizeBytes: 50_000);
        await MediaIntegrationTestHelpers.CompleteUploadAsync(Client, init.MediaAssetId);

        var res = await MediaIntegrationTestHelpers.SendProcessedCallbackAsync(
            Client,
            init.MediaAssetId,
            new MediaProcessedRequestDto
            {
                ProcessedObjectKey = $"processed/{init.MediaAssetId}/no-secret.jpg",
                StoredSizeBytes = 40_000,
            },
            workerSecret: null);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task WorkerCallback_Returns401_WithInvalidWorkerSecret()
    {
        MediaTestAuthHelpers.LoginAs(Client, userId: 1);
        var init = await MediaIntegrationTestHelpers.InitUploadAsync(
            Client,
            MediaIntendedContext.Post,
            "image/jpeg",
            "bad-secret.jpg",
            sizeBytes: 50_000);
        await MediaIntegrationTestHelpers.CompleteUploadAsync(Client, init.MediaAssetId);

        var res = await MediaIntegrationTestHelpers.SendProcessedCallbackAsync(
            Client,
            init.MediaAssetId,
            new MediaProcessedRequestDto
            {
                ProcessedObjectKey = $"processed/{init.MediaAssetId}/bad-secret.jpg",
                StoredSizeBytes = 40_000,
            },
            workerSecret: "wrong-secret");

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetContent_Returns200_ForLinkedPostMedia_WithoutAuth()
    {
        const long userId = 1;
        MediaTestAuthHelpers.LoginAs(Client, userId);
        var mediaAssetId = await MediaIntegrationTestHelpers.UploadAndMarkReadyAsync(
            Client,
            MediaIntendedContext.Post,
            "image/jpeg",
            "content.jpg",
            declaredSizeBytes: 10_000,
            storedSizeBytes: 8_000);
        Factory.FakeStorage.SeedObject($"processed/{mediaAssetId}/content.jpg");
        await MediaIntegrationTestHelpers.LinkToPostAsync(Client, postId: 42, userId, [mediaAssetId]);

        Client.DefaultRequestHeaders.Authorization = null;
        var res = await Client.GetAsync($"/api/media/{mediaAssetId}/content", TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
    }

    [Fact]
    public async Task LinkToPost_Returns400_WhenVideoTotalExceeded()
    {
        const long userId = 1;
        MediaTestAuthHelpers.LoginAs(Client, userId);
        var mediaAssetIds = new List<long>();
        for (var i = 0; i < 6; i++)
        {
            mediaAssetIds.Add(await MediaIntegrationTestHelpers.UploadAndMarkReadyAsync(
                Client,
                MediaIntendedContext.Post,
                "video/mp4",
                $"video-{i}.mp4",
                declaredSizeBytes: MediaIntegrationTestHelpers.PostVideoPerFileBytes,
                storedSizeBytes: MediaIntegrationTestHelpers.PostVideoPerFileBytes));
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, "/internal/media/link/post")
        {
            Content = JsonContent.Create(new LinkPostMediaRequestDto(99, userId, mediaAssetIds)),
        };
        message.Headers.Add(
            Media.Security.InternalServiceAuthorizationFilter.HeaderName,
            MediaWebApplicationFactory.TestInternalServiceSecret);
        var res = await Client.SendAsync(message, TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.BadRequest);
        await IntegrationAssertions.AssertProblemDetailContainsAsync(res, "total video storage limit");
    }

    private async Task<(IDatabase Database, RedisKey StreamKey, long LengthBefore)> GetMediaStreamLengthAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var redisOptions = scope.ServiceProvider.GetRequiredService<IOptions<RedisOptions>>().Value;
        Assert.False(string.IsNullOrWhiteSpace(redisOptions.ConnectionString));
        var multiplexer = await ConnectionMultiplexer.ConnectAsync(redis.ConnectionString);
        var streamKey = (RedisKey)MediaIntegrationTestHelpers.GetMediaUploadedStreamKey(redisOptions);
        var database = multiplexer.GetDatabase();
        var lengthBefore = await database.StreamLengthAsync(streamKey);
        return (database, streamKey, lengthBefore);
    }
}

[Collection(MediaIntegrationTestCollection.Name)]
public sealed class MediaIntegrationPostgresTests(PostgresTestcontainerFixture postgres)
    : IntegrationTestBase(postgres)
{
    [Fact]
    public async Task DeleteUnlinked_Returns204_ForReadyAsset()
    {
        MediaTestAuthHelpers.LoginAs(Client, userId: 1);
        var mediaAssetId = await MediaIntegrationTestHelpers.UploadAndMarkReadyAsync(
            Client,
            MediaIntendedContext.Post,
            "image/jpeg",
            "delete-me.jpg",
            declaredSizeBytes: 5_000,
            storedSizeBytes: 4_000);

        var res = await Client.DeleteAsync($"/api/media/{mediaAssetId}", TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NoContent);
        var getRes = await Client.GetAsync($"/api/media/{mediaAssetId}", TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(getRes, HttpStatusCode.NotFound);
    }
}
