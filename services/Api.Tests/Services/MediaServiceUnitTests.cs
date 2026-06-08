using Api.Domain.Media;
using Api.Domain.Media.Domain;
using Api.Domain.Media.Dto;
using Api.Domain.Media.Repository;
using Api.Domain.Media.Service;
using Api.Domain.Users.Domain;
using Api.Domain.Users.Service;
using Api.Global.Config;
using Api.Global.Db;
using Api.Global.Events;
using Api.Global.Queue;
using Api.Tests.Infrastructure;
using Api.Tests.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Tests.Services;

public sealed class MediaServiceUnitTests
{
    [Fact]
    public void BuildObjectKey_IncludesUserIdAndSafeFileName()
    {
        // Act
        var key = MediaService.BuildObjectKey(42, "../escape/video.mp4");

        // Assert
        Assert.StartsWith("raw/42/", key, StringComparison.Ordinal);
        Assert.EndsWith("/video.mp4", key, StringComparison.Ordinal);
        Assert.DoesNotContain("..", key, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompleteUploadAsync_EnqueuesMediaUploadedJob_WithTargetMaxFromLimits()
    {
        // Arrange
        var db = CreateInMemoryDb();
        var storage = new FakeMediaStorage();
        var workQueue = new FakeWorkQueue();
        var service = CreateMediaService(db, storage, workQueue);
        var asset = await SeedPendingUploadAsync(db, storage, userId: 1, objectKey: "raw/1/video.mp4");

        // Act
        var result = await service.CompleteUploadAsync(asset.Id);

        // Assert
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
        // Arrange
        var db = CreateInMemoryDb();
        var storage = new FakeMediaStorage();
        var service = CreateMediaService(db, storage);
        var asset = await SeedPendingUploadAsync(db, storage, userId: 1, objectKey: "raw/1/video.mp4");
        asset.MarkProcessing();
        await db.SaveChangesAsync();

        // Act
        await service.ReportProcessedAsync(asset.Id, new MediaProcessedRequestDto
        {
            ProcessedObjectKey = "processed/1/video.mp4",
            StoredSizeBytes = 400,
        });

        // Assert
        var updated = await db.MediaAssets.FindAsync(asset.Id);
        Assert.NotNull(updated);
        Assert.Equal(MediaProcessingStatus.Ready, updated.ProcessingStatus);
        Assert.Equal("processed/1/video.mp4", updated.ProcessedObjectKey);
        Assert.Equal(400, updated.StoredSizeBytes);
    }

    [Fact]
    public async Task ReportProcessedAsync_IsIdempotent_WhenAssetAlreadyReady()
    {
        // Arrange
        var db = CreateInMemoryDb();
        var storage = new FakeMediaStorage();
        var service = CreateMediaService(db, storage);
        var asset = await SeedPendingUploadAsync(db, storage, userId: 1, objectKey: "raw/1/video.mp4");
        asset.MarkProcessing();
        asset.MarkReady("processed/1/video.mp4", 400);
        await db.SaveChangesAsync();

        // Act
        await service.ReportProcessedAsync(asset.Id, new MediaProcessedRequestDto
        {
            ProcessedObjectKey = "processed/1/video.mp4",
            StoredSizeBytes = 400,
        });

        // Assert
        var updated = await db.MediaAssets.FindAsync(asset.Id);
        Assert.NotNull(updated);
        Assert.Equal(MediaProcessingStatus.Ready, updated.ProcessingStatus);
        Assert.Equal("processed/1/video.mp4", updated.ProcessedObjectKey);
        Assert.Equal(400, updated.StoredSizeBytes);
    }

    [Fact]
    public async Task ReportProcessedAsync_IsIdempotent_WhenAssetAlreadyFailed()
    {
        // Arrange
        var db = CreateInMemoryDb();
        var storage = new FakeMediaStorage();
        var service = CreateMediaService(db, storage);
        var asset = await SeedPendingUploadAsync(db, storage, userId: 1, objectKey: "raw/1/video.mp4");
        asset.MarkProcessing();
        asset.MarkFailed("compression failed");
        await db.SaveChangesAsync();

        // Act
        await service.ReportProcessedAsync(asset.Id, new MediaProcessedRequestDto
        {
            FailureReason = "compression failed again",
        });

        // Assert
        var updated = await db.MediaAssets.FindAsync(asset.Id);
        Assert.NotNull(updated);
        Assert.Equal(MediaProcessingStatus.Failed, updated.ProcessingStatus);
        Assert.Equal("compression failed", updated.FailureReason);
    }

    [Fact]
    public async Task ReportProcessedAsync_RejectsFailureCallback_WhenAssetAlreadyReady()
    {
        // Arrange
        var db = CreateInMemoryDb();
        var storage = new FakeMediaStorage();
        var service = CreateMediaService(db, storage);
        var asset = await SeedPendingUploadAsync(db, storage, userId: 1, objectKey: "raw/1/video.mp4");
        asset.MarkProcessing();
        asset.MarkReady("processed/1/video.mp4", 400);
        await db.SaveChangesAsync();

        // Act + Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.ReportProcessedAsync(asset.Id, new MediaProcessedRequestDto
        {
            FailureReason = "late failure",
        }));
        Assert.Contains("ready", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReportProcessedAsync_MarksAssetFailed_WhenFailureReasonProvided()
    {
        // Arrange
        var db = CreateInMemoryDb();
        var storage = new FakeMediaStorage();
        var service = CreateMediaService(db, storage);
        var asset = await SeedPendingUploadAsync(db, storage, userId: 1, objectKey: "raw/1/video.mp4");
        asset.MarkProcessing();
        await db.SaveChangesAsync();

        // Act
        await service.ReportProcessedAsync(asset.Id, new MediaProcessedRequestDto
        {
            FailureReason = "compression failed",
        });

        // Assert
        var updated = await db.MediaAssets.FindAsync(asset.Id);
        Assert.NotNull(updated);
        Assert.Equal(MediaProcessingStatus.Failed, updated.ProcessingStatus);
        Assert.Equal("compression failed", updated.FailureReason);
    }

    [Fact]
    public async Task CompleteUploadAsync_RejectsWhenBlobMissing()
    {
        // Arrange
        var db = CreateInMemoryDb();
        var storage = new FakeMediaStorage();
        var workQueue = new FakeWorkQueue();
        var service = CreateMediaService(db, storage, workQueue);
        var asset = await SeedPendingUploadAsync(db, storage, userId: 1, objectKey: "raw/1/missing.mp4", seedBlob: false);

        // Act + Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.CompleteUploadAsync(asset.Id));
        Assert.Contains("not found in storage", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(workQueue.GetEnqueuedJobs());
    }

    [Fact]
    public async Task LinkToPostAsync_LinksReadyAssetsToPost()
    {
        // Arrange
        var db = CreateInMemoryDb();
        var service = CreateMediaService(db, new FakeMediaStorage());
        var asset = await SeedReadyPostAssetAsync(db, storedSizeBytes: 1_000);

        // Act
        await service.LinkToPostAsync(postId: 99, uploaderUserId: asset.UploaderId!.Value, [asset.Id]);

        // Assert
        var updated = await db.MediaAssets.FindAsync(asset.Id);
        Assert.NotNull(updated);
        Assert.Equal(99, updated.PostId);
    }

    [Fact]
    public async Task LinkToPostAsync_RejectsWhenVideoTotalExceeded()
    {
        // Arrange
        var db = CreateInMemoryDb();
        var service = CreateMediaService(db, new FakeMediaStorage());
        const long maxPerFile = 2L * 1024 * 1024 * 1024;
        var assets = new List<MediaAsset>
        {
            await SeedReadyPostAssetAsync(db, kind: MediaKind.Video, storedSizeBytes: maxPerFile),
        };
        for (var i = 0; i < 5; i++)
        {
            assets.Add(await SeedReadyPostAssetAsync(
                db,
                uploaderId: assets[0].UploaderId,
                kind: MediaKind.Video,
                storedSizeBytes: maxPerFile,
                createUser: false));
        }

        // Act + Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.LinkToPostAsync(
                postId: 99,
                uploaderUserId: assets[0].UploaderId!.Value,
                assets.Select(asset => asset.Id).ToList()));
        Assert.Contains("total video storage limit", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetMediaForCommentAsync_ReturnsNull_WhenNoMediaLinked()
    {
        // Arrange
        var db = CreateInMemoryDb();
        var service = CreateMediaService(db, new FakeMediaStorage());

        // Act
        var media = await service.GetMediaForCommentAsync(commentId: 42);

        // Assert
        Assert.Null(media);
    }

    [Fact]
    public async Task GetMediaForCommentAsync_ThrowsWhenMultipleAssetsLinked()
    {
        // Arrange
        var db = CreateInMemoryDb();
        var service = CreateMediaService(db, new FakeMediaStorage());
        var user = new User("dup@example.com", "hash", "dup");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var first = MediaAsset.CreatePendingUpload(user.Id, MediaIntendedContext.Comment, MediaKind.Image, "image/jpeg", "a.jpg", "raw/a.jpg", 10);
        first.LinkToComment(5);
        first.MarkReady("processed/a.jpg", 8);
        var second = MediaAsset.CreatePendingUpload(user.Id, MediaIntendedContext.Comment, MediaKind.Image, "image/jpeg", "b.jpg", "raw/b.jpg", 10);
        second.LinkToComment(5);
        second.MarkReady("processed/b.jpg", 8);
        db.MediaAssets.AddRange(first, second);
        await db.SaveChangesAsync();

        // Act + Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetMediaForCommentAsync(5));
    }

    [Fact]
    public async Task LinkToCommentAsync_RejectsWhenAssetNotReady()
    {
        // Arrange
        var db = CreateInMemoryDb();
        var service = CreateMediaService(db, new FakeMediaStorage());
        var user = new User("commenter@example.com", "hash", "commenter");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var asset = MediaAsset.CreatePendingUpload(
            user.Id,
            MediaIntendedContext.Comment,
            MediaKind.Image,
            "image/jpeg",
            "comment.jpg",
            "raw/comment/pending.jpg",
            100);
        db.MediaAssets.Add(asset);
        await db.SaveChangesAsync();

        // Act + Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.LinkToCommentAsync(commentId: 10, uploaderUserId: user.Id, asset.Id));
        Assert.Contains("ready", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteBlobStorageForPostAsync_RemovesOnlyPostLinkedMedia()
    {
        // Arrange
        var db = CreateInMemoryDb();
        var storage = new FakeMediaStorage();
        var service = CreateMediaService(db, storage);
        await SeedPostAndCommentMediaAsync(db, storage);

        // Act
        await service.DeleteBlobStorageForPostAsync(postId: 1);

        // Assert
        Assert.Equal(["raw/post/original.jpg", "raw/post/processed.jpg"], storage.GetDeletedObjectKeys());
        Assert.True(await storage.ObjectExistsAsync("raw/comment/original.jpg", TestContext.Current.CancellationToken));
        Assert.True(await storage.ObjectExistsAsync("raw/comment/processed.jpg", TestContext.Current.CancellationToken));
    }

    private static AppDbContext CreateInMemoryDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static MediaService CreateMediaService(
        AppDbContext db,
        FakeMediaStorage storage,
        FakeWorkQueue? workQueue = null)
    {
        workQueue ??= new FakeWorkQueue();
        var mediaOptions = Options.Create(new MediaOptions
        {
            Enabled = true,
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
        });
        var http = new FakeHttpContextAccessor("1");
        var userRepository = new FakeUserRepository();
        var nicknameCache = new NicknameCacheService(userRepository, new FakeDistributedCache());
        var userService = new UserService(
            userRepository,
            db,
            new Lazy<Api.Domain.Posts.Service.PostService>(() => null!),
            new Lazy<Api.Domain.Comments.Service.CommentService>(() => null!),
            new Lazy<MediaService>(() => null!),
            new Lazy<Api.Domain.Chat.Service.ChatMessageService>(() => null!),
            new Lazy<Api.Domain.Chat.Service.ChatRoomService>(() => null!),
            new Lazy<Api.Domain.Groups.Service.GroupMembershipService>(() => null!),
            http,
            nicknameCache,
            new NoOpEventPublisher());

        return new MediaService(
            new MediaAssetRepository(db),
            storage,
            new MediaLimitPolicy(mediaOptions),
            userService,
            workQueue,
            mediaOptions,
            http);
    }

    private static async Task<MediaAsset> SeedReadyPostAssetAsync(
        AppDbContext db,
        long? uploaderId = null,
        MediaKind kind = MediaKind.Video,
        long storedSizeBytes = 400,
        bool createUser = true)
    {
        var asset = await SeedPendingUploadAsync(
            db,
            new FakeMediaStorage(),
            uploaderId ?? 1,
            $"raw/{uploaderId ?? 1}/{kind.ToString().ToLowerInvariant()}.bin",
            seedBlob: false,
            createUser: createUser);
        asset.MarkProcessing();
        asset.MarkReady($"processed/{asset.UploaderId}/{kind.ToString().ToLowerInvariant()}.bin", storedSizeBytes);
        await db.SaveChangesAsync();
        return asset;
    }

    private static async Task<MediaAsset> SeedPendingUploadAsync(
        AppDbContext db,
        FakeMediaStorage storage,
        long userId,
        string objectKey,
        bool seedBlob = true,
        bool createUser = true)
    {
        long uploaderId = userId;
        if (createUser)
        {
            var user = new User($"user{userId}@example.com", "hash", $"user{userId}");
            db.Users.Add(user);
            await db.SaveChangesAsync();
            uploaderId = user.Id;
        }

        var asset = MediaAsset.CreatePendingUpload(
            uploaderId,
            MediaIntendedContext.Post,
            MediaKind.Video,
            "video/mp4",
            "video.mp4",
            objectKey,
            500);
        db.MediaAssets.Add(asset);
        await db.SaveChangesAsync();

        if (seedBlob) storage.SeedObject(objectKey);

        return asset;
    }

    private static async Task SeedPostAndCommentMediaAsync(AppDbContext db, FakeMediaStorage storage)
    {
        var user = new User("tester@example.com", "hash", "tester");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var postMedia = MediaAsset.CreatePendingUpload(
            user.Id,
            MediaIntendedContext.Post,
            MediaKind.Image,
            "image/jpeg",
            "post.jpg",
            "raw/post/original.jpg",
            100);
        postMedia.LinkToPost(1);
        postMedia.MarkReady("raw/post/processed.jpg", 80);

        var commentMedia = MediaAsset.CreatePendingUpload(
            user.Id,
            MediaIntendedContext.Comment,
            MediaKind.Image,
            "image/jpeg",
            "comment.jpg",
            "raw/comment/original.jpg",
            100);
        commentMedia.LinkToComment(10);
        commentMedia.MarkReady("raw/comment/processed.jpg", 80);

        db.MediaAssets.AddRange(postMedia, commentMedia);
        await db.SaveChangesAsync();

        storage.SeedObject("raw/post/original.jpg");
        storage.SeedObject("raw/post/processed.jpg");
        storage.SeedObject("raw/comment/original.jpg");
        storage.SeedObject("raw/comment/processed.jpg");
    }
}
