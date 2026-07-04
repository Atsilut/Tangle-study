using Media.Dto;
using Media.Config;
using Media.Db;
using Media.Queue;
using Media.Repository;
using Media.Service;
using Media.Storage;
using Media.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Media.Tests.Services;

public sealed class MediaServiceUnitTests
{
    [Fact]
    public void CreateServiceClient_ParsesAzuriteConnectionString()
    {
        const string connectionString =
            "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://azurite:10000/devstoreaccount1;";

        var client = AzureBlobMediaStorage.CreateServiceClient(connectionString);

        Assert.Equal("devstoreaccount1", client.AccountName);
        Assert.StartsWith("http://azurite:10000/devstoreaccount1", client.Uri.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildObjectKey_IncludesUserIdAndSafeFileName()
    {
        var key = MediaService.BuildObjectKey(42, "../escape/video.mp4");
        Assert.StartsWith("raw/42/", key, StringComparison.Ordinal);
        Assert.EndsWith("/video.mp4", key, StringComparison.Ordinal);
        Assert.DoesNotContain("..", key, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompleteUploadAsync_EnqueuesMediaUploadedJob_WithTargetMaxFromLimits()
    {
        var db = CreateInMemoryDb();
        var storage = new FakeMediaStorage();
        var workQueue = new FakeWorkQueue();
        var service = CreateMediaService(db, storage, workQueue);
        var asset = await SeedPendingUploadAsync(db, storage, userId: 1, objectKey: "raw/1/video.mp4");

        var result = await service.CompleteUploadAsync(asset.Id);

        Assert.Equal(MediaProcessingStatus.Processing, result.ProcessingStatus);
        var job = Assert.Single(workQueue.GetEnqueuedJobs());
        Assert.Equal(WorkQueueStreams.MediaUploaded, job.StreamKey);
        var payload = Assert.IsType<MediaUploadedJob>(job.Payload);
        Assert.Equal(asset.Id, payload.MediaAssetId);
        Assert.Equal(nameof(MediaIntendedContext.Post), payload.IntendedContext);
        Assert.Equal(nameof(MediaKind.Video), payload.Kind);
        Assert.Equal("video/mp4", payload.MimeType);
        Assert.Equal("raw/1/video.mp4", payload.OriginalObjectKey);
        Assert.Equal(500, payload.OriginalSizeBytes);
        Assert.Equal(2L * 1024 * 1024 * 1024, payload.TargetMaxBytes);
    }

    [Fact]
    public async Task ReportProcessedAsync_MarksAssetReady_WhenWithinStorageLimit()
    {
        var db = CreateInMemoryDb();
        var storage = new FakeMediaStorage();
        var service = CreateMediaService(db, storage);
        var asset = await SeedPendingUploadAsync(db, storage, userId: 1, objectKey: "raw/1/video.mp4");
        asset.MarkProcessing();
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await service.ReportProcessedAsync(asset.Id, new MediaProcessedRequestDto
        {
            ProcessedObjectKey = "processed/1/video.mp4",
            StoredSizeBytes = 400,
        });

        var updated = await db.MediaAssets.FindAsync([asset.Id], TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal(MediaProcessingStatus.Ready, updated.ProcessingStatus);
        Assert.Equal("processed/1/video.mp4", updated.ProcessedObjectKey);
        Assert.Equal(400, updated.StoredSizeBytes);
    }

    [Fact]
    public async Task ReportProcessedAsync_IsIdempotent_WhenAssetAlreadyReady()
    {
        var db = CreateInMemoryDb();
        var service = CreateMediaService(db, new FakeMediaStorage());
        var asset = await SeedPendingUploadAsync(db, new FakeMediaStorage(), userId: 1, objectKey: "raw/1/video.mp4", seedBlob: false);
        asset.MarkProcessing();
        asset.MarkReady("processed/1/video.mp4", 400);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await service.ReportProcessedAsync(asset.Id, new MediaProcessedRequestDto
        {
            ProcessedObjectKey = "processed/1/video.mp4",
            StoredSizeBytes = 400,
        });

        var updated = await db.MediaAssets.FindAsync([asset.Id], TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal(MediaProcessingStatus.Ready, updated.ProcessingStatus);
    }

    [Fact]
    public async Task ReportProcessedAsync_MarksAssetFailed_WhenFailureReasonProvided()
    {
        var db = CreateInMemoryDb();
        var service = CreateMediaService(db, new FakeMediaStorage());
        var asset = await SeedPendingUploadAsync(db, new FakeMediaStorage(), userId: 1, objectKey: "raw/1/video.mp4", seedBlob: false);
        asset.MarkProcessing();
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await service.ReportProcessedAsync(asset.Id, new MediaProcessedRequestDto
        {
            FailureReason = "compression failed",
        });

        var updated = await db.MediaAssets.FindAsync([asset.Id], TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal(MediaProcessingStatus.Failed, updated.ProcessingStatus);
        Assert.Equal("compression failed", updated.FailureReason);
    }

    [Fact]
    public async Task CompleteUploadAsync_RejectsWhenBlobMissing()
    {
        var db = CreateInMemoryDb();
        var storage = new FakeMediaStorage();
        var workQueue = new FakeWorkQueue();
        var service = CreateMediaService(db, storage, workQueue);
        var asset = await SeedPendingUploadAsync(db, storage, userId: 1, objectKey: "raw/1/missing.mp4", seedBlob: false);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.CompleteUploadAsync(asset.Id));
        Assert.Contains("not found in storage", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(workQueue.GetEnqueuedJobs());
    }

    [Fact]
    public async Task LinkToPostAsync_LinksReadyAssetsToPost()
    {
        var db = CreateInMemoryDb();
        var service = CreateMediaService(db, new FakeMediaStorage());
        var asset = await SeedReadyPostAssetAsync(db);

        await service.LinkToPostAsync(postId: 99, uploaderUserId: asset.UploaderId!.Value, [asset.Id]);

        var updated = await db.MediaAssets.FindAsync([asset.Id], TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal(99, updated.PostId);
    }

    [Fact]
    public async Task LinkToPostAsync_RejectsWhenVideoTotalExceeded()
    {
        var db = CreateInMemoryDb();
        var service = CreateMediaService(db, new FakeMediaStorage());
        const long maxPerFile = 2L * 1024 * 1024 * 1024;
        var assets = new List<MediaAsset> { await SeedReadyPostAssetAsync(db, kind: MediaKind.Video, storedSizeBytes: maxPerFile) };
        for (var i = 0; i < 5; i++)
            assets.Add(await SeedReadyPostAssetAsync(db, uploaderId: assets[0].UploaderId!.Value, kind: MediaKind.Video, storedSizeBytes: maxPerFile));

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.LinkToPostAsync(
                postId: 99,
                uploaderUserId: assets[0].UploaderId!.Value,
                [.. assets.Select(asset => asset.Id)]));
        Assert.Contains("total video storage limit", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetMediaForCommentAsync_ReturnsNull_WhenNoMediaLinked()
    {
        var db = CreateInMemoryDb();
        var service = CreateMediaService(db, new FakeMediaStorage());
        var media = await service.GetMediaForCommentAsync(commentId: 42);
        Assert.Null(media);
    }

    [Fact]
    public async Task GetMediaByPostIdsAsync_ReturnsMediaGroupedByPostId()
    {
        var db = CreateInMemoryDb();
        var service = CreateMediaService(db, new FakeMediaStorage());
        const long uploaderId = 1;

        var postOneFirst = MediaAsset.CreatePendingUpload(uploaderId, MediaIntendedContext.Post, MediaKind.Image, "image/jpeg", "a.jpg", "raw/a.jpg", 10);
        postOneFirst.LinkToPost(1);
        postOneFirst.MarkReady("processed/a.jpg", 8);
        var postOneSecond = MediaAsset.CreatePendingUpload(uploaderId, MediaIntendedContext.Post, MediaKind.Image, "image/jpeg", "b.jpg", "raw/b.jpg", 10);
        postOneSecond.LinkToPost(1);
        postOneSecond.MarkReady("processed/b.jpg", 8);
        var postTwo = MediaAsset.CreatePendingUpload(uploaderId, MediaIntendedContext.Post, MediaKind.Image, "image/jpeg", "c.jpg", "raw/c.jpg", 10);
        postTwo.LinkToPost(2);
        postTwo.MarkReady("processed/c.jpg", 8);
        db.MediaAssets.AddRange(postOneFirst, postOneSecond, postTwo);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var mediaByPostId = await service.GetMediaByPostIdsAsync([1, 2, 99]);

        Assert.Equal(2, mediaByPostId.Count);
        Assert.Equal(2, mediaByPostId[1].Count);
        Assert.Single(mediaByPostId[2]);
        Assert.False(mediaByPostId.ContainsKey(99));
    }

    [Fact]
    public async Task DeleteBlobStorageForPostAsync_RemovesOnlyPostLinkedMedia()
    {
        var db = CreateInMemoryDb();
        var storage = new FakeMediaStorage();
        var service = CreateMediaService(db, storage);
        await SeedPostAndCommentMediaAsync(db, storage);

        await service.DeleteBlobStorageForPostAsync(postId: 1);

        Assert.Equal(["raw/post/original.jpg", "raw/post/processed.jpg"], storage.GetDeletedObjectKeys());
        Assert.True(await storage.ObjectExistsAsync("raw/comment/original.jpg", TestContext.Current.CancellationToken));
    }

    private static MediaDbContext CreateInMemoryDb() =>
        new(new DbContextOptionsBuilder<MediaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static MediaService CreateMediaService(
        MediaDbContext db,
        FakeMediaStorage storage,
        FakeWorkQueue? workQueue = null,
        string userId = "1")
    {
        workQueue ??= new FakeWorkQueue();
        var mediaOptions = Options.Create(CreateTestMediaOptions());
        var http = new FakeHttpContextAccessor(userId);
        var serviceProvider = new ServiceCollection()
            .AddSingleton<IMediaStorage>(storage)
            .BuildServiceProvider();

        return new MediaService(
            new MediaAssetRepository(db),
            serviceProvider,
            new MediaLimitPolicy(mediaOptions),
            new AllowAllMonolithAccessClient(),
            new AllowAllCommunityAccessClient(),
            new AllowAllChatAccessClient(),
            workQueue,
            mediaOptions,
            http);
    }

    private static MediaOptions CreateTestMediaOptions() => new()
    {
        IngressMultiplier = 3,
        Post = new MediaContextLimitOptions
        {
            VideoPerFileBytes = 2L * 1024 * 1024 * 1024,
            VideoTotalBytes = 10L * 1024 * 1024 * 1024,
            ImagePerFileBytes = 150L * 1024 * 1024,
            ImageTotalBytes = 3L * 1024 * 1024 * 1024,
        },
        Comment = new MediaContextLimitOptions
        {
            VideoPerFileBytes = 150L * 1024 * 1024,
            ImagePerFileBytes = 75L * 1024 * 1024,
        },
        ChatMessage = new MediaContextLimitOptions
        {
            VideoPerFileBytes = 150L * 1024 * 1024,
            ImagePerFileBytes = 75L * 1024 * 1024,
        },
    };

    private static async Task<MediaAsset> SeedReadyPostAssetAsync(
        MediaDbContext db,
        long uploaderId = 1,
        MediaKind kind = MediaKind.Video,
        long storedSizeBytes = 400)
    {
        var asset = await SeedPendingUploadAsync(db, new FakeMediaStorage(), uploaderId, $"raw/{uploaderId}/{kind.ToString().ToLowerInvariant()}.bin", seedBlob: false);
        asset.MarkProcessing();
        asset.MarkReady($"processed/{uploaderId}/{kind.ToString().ToLowerInvariant()}.bin", storedSizeBytes);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return asset;
    }

    private static async Task<MediaAsset> SeedPendingUploadAsync(
        MediaDbContext db,
        FakeMediaStorage storage,
        long userId,
        string objectKey,
        bool seedBlob = true)
    {
        var asset = MediaAsset.CreatePendingUpload(
            userId,
            MediaIntendedContext.Post,
            MediaKind.Video,
            "video/mp4",
            "video.mp4",
            objectKey,
            500);
        db.MediaAssets.Add(asset);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        if (seedBlob) storage.SeedObject(objectKey);
        return asset;
    }

    private static async Task SeedPostAndCommentMediaAsync(MediaDbContext db, FakeMediaStorage storage)
    {
        const long uploaderId = 1;
        var postMedia = MediaAsset.CreatePendingUpload(uploaderId, MediaIntendedContext.Post, MediaKind.Image, "image/jpeg", "post.jpg", "raw/post/original.jpg", 100);
        postMedia.LinkToPost(1);
        postMedia.MarkReady("raw/post/processed.jpg", 80);

        var commentMedia = MediaAsset.CreatePendingUpload(uploaderId, MediaIntendedContext.Comment, MediaKind.Image, "image/jpeg", "comment.jpg", "raw/comment/original.jpg", 100);
        commentMedia.LinkToComment(10);
        commentMedia.MarkReady("raw/comment/processed.jpg", 80);

        db.MediaAssets.AddRange(postMedia, commentMedia);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        storage.SeedObject("raw/post/original.jpg");
        storage.SeedObject("raw/post/processed.jpg");
        storage.SeedObject("raw/comment/original.jpg");
        storage.SeedObject("raw/comment/processed.jpg");
    }
}
