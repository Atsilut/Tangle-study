using System.Net;
using System.Net.Http.Json;
using Api.Domain.Comments.Dto;
using Api.Domain.Media.Domain;
using Api.Domain.Media.Dto;
using Api.Domain.Posts.Dto;
using Api.Domain.Users.Dto;
using Api.Global.Config;
using Api.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Api.Tests.Controllers;

[Collection(RedisRealtimeIntegrationTestCollection.Name)]
public sealed class MediaIntegrationTests(
    PostgresTestcontainerFixture postgres,
    RedisTestcontainerFixture redis)
    : IntegrationTestBase(postgres, redisEnabled: true, redisConnectionString: redis.ConnectionString)
{
    // --- Upload-init ingress ---

    [Fact]
    public async Task InitUpload_Returns400_WhenDeclaredSizeExceedsIngressLimit()
    {
        const string testMethodName = nameof(InitUpload_Returns400_WhenDeclaredSizeExceedsIngressLimit);

        // Arrange
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);
        const long ingressLimit = MediaIntegrationTestHelpers.PostVideoPerFileBytes * MediaIntegrationTestHelpers.IngressMultiplier;
        var req = new MediaUploadInitRequestDto
        {
            IntendedContext = MediaIntendedContext.Post,
            MimeType = "video/mp4",
            FileName = "too-large.mp4",
            SizeBytes = ingressLimit + 1,
        };

        // Act
        var res = await Client.PostAsJsonAsync("/api/media/upload-init", req, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.BadRequest);
        await IntegrationAssertions.AssertProblemDetailContainsAsync(res, "upload limit");
    }

    // --- Complete + Redis stream ---

    [Fact]
    public async Task CompleteUpload_EnqueuesMediaUploadedJob_ToRedisStream()
    {
        const string testMethodName = nameof(CompleteUpload_EnqueuesMediaUploadedJob_ToRedisStream);

        // Arrange
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);
        var init = await MediaIntegrationTestHelpers.InitUploadAsync(
            Client,
            MediaIntendedContext.Post,
            "video/mp4",
            "stream-test.mp4",
            sizeBytes: 1_000);
        var (database, streamKey, lengthBefore) = await GetMediaStreamLengthAsync();

        // Act
        var completed = await MediaIntegrationTestHelpers.CompleteUploadAsync(Client, init.MediaAssetId);

        // Assert
        Assert.Equal(MediaProcessingStatus.Processing, completed.ProcessingStatus);
        var lengthAfter = await database.StreamLengthAsync(streamKey);
        Assert.Equal(lengthBefore + 1, lengthAfter);
        await database.Multiplexer.CloseAsync();
    }

    [Fact]
    public async Task CompleteUpload_Returns400_WhenBlobMissing()
    {
        const string testMethodName = nameof(CompleteUpload_Returns400_WhenBlobMissing);

        // Arrange
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);
        var init = await MediaIntegrationTestHelpers.InitUploadAsync(
            Client,
            MediaIntendedContext.Post,
            "video/mp4",
            "missing-blob.mp4",
            sizeBytes: 1_000);
        await using var scope = Factory.Services.CreateAsyncScope();
        MediaIntegrationTestHelpers.GetFakeStorage(scope.ServiceProvider).RemoveObject(init.ObjectKey);

        // Act
        var res = await Client.PostAsync($"/api/media/{init.MediaAssetId}/complete", null, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.BadRequest);
        await IntegrationAssertions.AssertProblemDetailContainsAsync(res, "not found in storage");
    }

    // --- Worker callback ---

    [Fact]
    public async Task WorkerCallback_MarksAssetReady()
    {
        const string testMethodName = nameof(WorkerCallback_MarksAssetReady);

        // Arrange
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);
        var init = await MediaIntegrationTestHelpers.InitUploadAsync(
            Client,
            MediaIntendedContext.Post,
            "image/jpeg",
            "callback.jpg",
            sizeBytes: 50_000);
        await MediaIntegrationTestHelpers.CompleteUploadAsync(Client, init.MediaAssetId);

        // Act
        await MediaIntegrationTestHelpers.MarkProcessedReadyAsync(
            Client,
            init.MediaAssetId,
            $"processed/{init.MediaAssetId}/callback.jpg",
            storedSizeBytes: 40_000);

        // Assert
        var getRes = await Client.GetAsync($"/api/media/{init.MediaAssetId}", TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(getRes, HttpStatusCode.OK);
        var asset = await getRes.Content.ReadFromJsonAsync<MediaAssetGetResponseDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(asset);
        Assert.Equal(MediaProcessingStatus.Ready, asset.ProcessingStatus);
        Assert.Equal(40_000, asset.StoredSizeBytes);
    }

    // --- Attach to post ---

    [Fact]
    public async Task CreatePost_WithReadyMedia_ReturnsMediaInGet()
    {
        const string testMethodName = nameof(CreatePost_WithReadyMedia_ReturnsMediaInGet);

        // Arrange
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);
        var mediaAssetId = await MediaIntegrationTestHelpers.UploadAndMarkReadyAsync(
            Client,
            MediaIntendedContext.Post,
            "image/jpeg",
            "post-attach.jpg",
            declaredSizeBytes: 10_000,
            storedSizeBytes: 8_000);
        var req = new PostCreateRequestDto
        {
            Title = $"{testMethodName} title",
            Content = $"{testMethodName} content",
            MediaAssetIds = [mediaAssetId],
        };

        // Act
        var createRes = await Client.PostAsJsonAsync("/api/posts", req, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(createRes, HttpStatusCode.Created);
        var listRes = await Client.GetAsync("/api/posts", TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(listRes, HttpStatusCode.OK);
        var posts = await listRes.Content.ReadFromJsonAsync<List<PostGetResponseDto>>(TestContext.Current.CancellationToken);
        var post = Assert.Single(posts!, p => p.Title == req.Title);
        var media = Assert.Single(post.Media);
        Assert.Equal(mediaAssetId, media.Id);
        Assert.Equal(MediaProcessingStatus.Ready, media.ProcessingStatus);
        Assert.Equal(8_000, media.StoredSizeBytes);
    }

    [Fact]
    public async Task GetContent_Returns200_ForLinkedPostMedia_WithoutAuth()
    {
        const string testMethodName = nameof(GetContent_Returns200_ForLinkedPostMedia_WithoutAuth);

        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);
        var mediaAssetId = await MediaIntegrationTestHelpers.UploadAndMarkReadyAsync(
            Client,
            MediaIntendedContext.Post,
            "image/jpeg",
            "content.jpg",
            declaredSizeBytes: 10_000,
            storedSizeBytes: 8_000);
        MediaIntegrationTestHelpers
            .GetFakeStorage(Factory.Services)
            .SeedObject($"processed/{mediaAssetId}/content.jpg");
        var req = new PostCreateRequestDto
        {
            Title = $"{testMethodName} title",
            Content = $"{testMethodName} content",
            MediaAssetIds = [mediaAssetId],
        };
        var createRes = await Client.PostAsJsonAsync("/api/posts", req, TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(createRes, HttpStatusCode.Created);

        Client.DefaultRequestHeaders.Authorization = null;

        var contentRes = await Client.GetAsync($"/api/media/{mediaAssetId}/content", TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(contentRes, HttpStatusCode.OK);
        Assert.Equal("image/jpeg", contentRes.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task UpdatePost_AddMedia_ReturnsMediaInGet()
    {
        const string testMethodName = nameof(UpdatePost_AddMedia_ReturnsMediaInGet);

        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);
        var createReq = new PostCreateRequestDto
        {
            Title = $"{testMethodName} title",
            Content = $"{testMethodName} content",
        };
        var createRes = await Client.PostAsJsonAsync("/api/posts", createReq, TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(createRes, HttpStatusCode.Created);
        var listRes = await Client.GetAsync("/api/posts", TestContext.Current.CancellationToken);
        var posts = await listRes.Content.ReadFromJsonAsync<List<PostGetResponseDto>>(TestContext.Current.CancellationToken);
        var post = Assert.Single(posts!, p => p.Title == createReq.Title);
        Assert.Empty(post.Media);

        var mediaAssetId = await MediaIntegrationTestHelpers.UploadAndMarkReadyAsync(
            Client,
            MediaIntendedContext.Post,
            "image/jpeg",
            "patch-add.jpg",
            declaredSizeBytes: 10_000,
            storedSizeBytes: 8_000);

        var patchReq = new PostPatchRequestDto
        {
            Id = post.Id,
            Title = createReq.Title,
            Content = createReq.Content,
            AddMediaAssetIds = [mediaAssetId],
        };
        var patchRes = await Client.PatchAsJsonAsync("/api/posts", patchReq, TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(patchRes, HttpStatusCode.OK);

        var getRes = await Client.GetAsync($"/api/posts/{post.Id}", TestContext.Current.CancellationToken);
        var updated = await getRes.Content.ReadFromJsonAsync<PostGetResponseDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        var media = Assert.Single(updated.Media);
        Assert.Equal(mediaAssetId, media.Id);
    }

    [Fact]
    public async Task UpdatePost_RemoveMedia_DeletesAsset()
    {
        const string testMethodName = nameof(UpdatePost_RemoveMedia_DeletesAsset);

        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);
        var mediaAssetId = await MediaIntegrationTestHelpers.UploadAndMarkReadyAsync(
            Client,
            MediaIntendedContext.Post,
            "image/jpeg",
            "patch-remove.jpg",
            declaredSizeBytes: 10_000,
            storedSizeBytes: 8_000);
        var createReq = new PostCreateRequestDto
        {
            Title = $"{testMethodName} title",
            Content = $"{testMethodName} content",
            MediaAssetIds = [mediaAssetId],
        };
        var createRes = await Client.PostAsJsonAsync("/api/posts", createReq, TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(createRes, HttpStatusCode.Created);
        var listRes = await Client.GetAsync("/api/posts", TestContext.Current.CancellationToken);
        var posts = await listRes.Content.ReadFromJsonAsync<List<PostGetResponseDto>>(TestContext.Current.CancellationToken);
        var post = Assert.Single(posts!, p => p.Title == createReq.Title);
        Assert.Single(post.Media);

        var patchReq = new PostPatchRequestDto
        {
            Id = post.Id,
            Title = createReq.Title,
            Content = createReq.Content,
            RemoveMediaAssetIds = [mediaAssetId],
        };
        var patchRes = await Client.PatchAsJsonAsync("/api/posts", patchReq, TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(patchRes, HttpStatusCode.OK);

        var getRes = await Client.GetAsync($"/api/posts/{post.Id}", TestContext.Current.CancellationToken);
        var updated = await getRes.Content.ReadFromJsonAsync<PostGetResponseDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Empty(updated.Media);

        var mediaRes = await Client.GetAsync($"/api/media/{mediaAssetId}", TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(mediaRes, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreatePost_Returns400_WhenVideoTotalExceeded()
    {
        const string testMethodName = nameof(CreatePost_Returns400_WhenVideoTotalExceeded);

        // Arrange
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);
        var mediaAssetIds = new List<long>();
        for (var i = 0; i < 6; i++)
        {
            mediaAssetIds.Add(await MediaIntegrationTestHelpers.UploadAndMarkReadyAsync(
                Client,
                MediaIntendedContext.Post,
                "video/mp4",
                $"video-{i}.mp4",
                declaredSizeBytes: 1_000,
                storedSizeBytes: MediaIntegrationTestHelpers.PostVideoPerFileBytes));
        }

        var req = new PostCreateRequestDto
        {
            Title = $"{testMethodName} title",
            Content = $"{testMethodName} content",
            MediaAssetIds = [.. mediaAssetIds],
        };

        // Act
        var res = await Client.PostAsJsonAsync("/api/posts", req, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.BadRequest);
        await IntegrationAssertions.AssertProblemDetailContainsAsync(res, "total video storage limit");
    }

    // --- Attach to comment ---

    [Fact]
    public async Task CreateComment_Returns400_WhenMediaAssetAlreadyLinked()
    {
        const string testMethodName = nameof(CreateComment_Returns400_WhenMediaAssetAlreadyLinked);

        // Arrange
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);
        var postReq = new PostCreateRequestDto
        {
            Title = $"{testMethodName} post",
            Content = $"{testMethodName} post content",
        };
        var postRes = await Client.PostAsJsonAsync("/api/posts", postReq, TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(postRes, HttpStatusCode.Created);
        var posts = await (await Client.GetAsync("/api/posts", TestContext.Current.CancellationToken))
            .Content.ReadFromJsonAsync<List<PostGetResponseDto>>(TestContext.Current.CancellationToken);
        var post = Assert.Single(posts!, p => p.Title == postReq.Title);

        var mediaAssetId = await MediaIntegrationTestHelpers.UploadAndMarkReadyAsync(
            Client,
            MediaIntendedContext.Comment,
            "image/jpeg",
            "comment.jpg",
            declaredSizeBytes: 5_000,
            storedSizeBytes: 4_000);

        var firstCommentReq = new CommentCreateRequestDto
        {
            PostId = post.Id,
            Content = $"{testMethodName} first",
            MediaAssetId = mediaAssetId,
        };
        var firstRes = await Client.PostAsJsonAsync("/api/comments", firstCommentReq, TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(firstRes, HttpStatusCode.Created);

        var secondCommentReq = new CommentCreateRequestDto
        {
            PostId = post.Id,
            Content = $"{testMethodName} second",
            MediaAssetId = mediaAssetId,
        };

        // Act
        var secondRes = await Client.PostAsJsonAsync("/api/comments", secondCommentReq, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(secondRes, HttpStatusCode.BadRequest);
        await IntegrationAssertions.AssertProblemDetailContainsAsync(secondRes, "already linked");
    }

    private async Task<(IDatabase Database, RedisKey StreamKey, long LengthBefore)> GetMediaStreamLengthAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var redisOptions = scope.ServiceProvider.GetRequiredService<IOptions<RedisOptions>>().Value;
        Assert.True(redisOptions.Enabled);
        var multiplexer = await ConnectionMultiplexer.ConnectAsync(redis.ConnectionString);
        var streamKey = MediaIntegrationTestHelpers.GetMediaUploadedStreamKey(redisOptions);
        var database = multiplexer.GetDatabase();
        var lengthBefore = await database.StreamLengthAsync(streamKey);
        return (database, streamKey, lengthBefore);
    }
}
