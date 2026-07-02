using Api.Domain.Chat.Service;
using Api.Domain.Comments.Service;
using Api.Domain.Groups.Service;
using Api.Domain.Location.Service;
using Api.Domain.Media;
using Api.Domain.Media.Domain;
using Api.Domain.Media.Dto;
using Api.Domain.Media.Repository;
using Api.Domain.Media.Service;
using Api.Domain.Media.Storage;
using Api.Domain.Posts.Service;
using Api.Domain.Users.Domain;
using Api.Domain.Users.Service;
using Api.Global.Config;
using Api.Global.Db;
using Api.Global.Events;
using Api.Global.Queue;
using Api.Tests.Infrastructure;
using Api.Tests.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Api.Tests.Services;

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
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await service.ReportProcessedAsync(asset.Id, new MediaProcessedRequestDto
        {
            ProcessedObjectKey = "processed/1/video.mp4",
            StoredSizeBytes = 400,
        });

        // Assert
        var updated = await db.MediaAssets.FindAsync([asset.Id], TestContext.Current.CancellationToken);
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
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await service.ReportProcessedAsync(asset.Id, new MediaProcessedRequestDto
        {
            ProcessedObjectKey = "processed/1/video.mp4",
            StoredSizeBytes = 400,
        });

        // Assert
        var updated = await db.MediaAssets.FindAsync([asset.Id], TestContext.Current.CancellationToken);
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
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await service.ReportProcessedAsync(asset.Id, new MediaProcessedRequestDto
        {
            FailureReason = "compression failed again",
        });

        // Assert
        var updated = await db.MediaAssets.FindAsync([asset.Id], TestContext.Current.CancellationToken);
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
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

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
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await service.ReportProcessedAsync(asset.Id, new MediaProcessedRequestDto
        {
            FailureReason = "compression failed",
        });

        // Assert
        var updated = await db.MediaAssets.FindAsync([asset.Id], TestContext.Current.CancellationToken);
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
        var updated = await db.MediaAssets.FindAsync([asset.Id], TestContext.Current.CancellationToken);
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
                [.. assets.Select(asset => asset.Id)]));
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
    public async Task GetMediaByPostIdsAsync_ReturnsMediaGroupedByPostId()
    {
        // Arrange
        var db = CreateInMemoryDb();
        var service = CreateMediaService(db, new FakeMediaStorage());
        var user = new User("poster@example.com", "hash", "poster");
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var postOneFirst = MediaAsset.CreatePendingUpload(user.Id, MediaIntendedContext.Post, MediaKind.Image, "image/jpeg", "a.jpg", "raw/a.jpg", 10);
        postOneFirst.LinkToPost(1);
        postOneFirst.MarkReady("processed/a.jpg", 8);
        var postOneSecond = MediaAsset.CreatePendingUpload(user.Id, MediaIntendedContext.Post, MediaKind.Image, "image/jpeg", "b.jpg", "raw/b.jpg", 10);
        postOneSecond.LinkToPost(1);
        postOneSecond.MarkReady("processed/b.jpg", 8);
        var postTwo = MediaAsset.CreatePendingUpload(user.Id, MediaIntendedContext.Post, MediaKind.Image, "image/jpeg", "c.jpg", "raw/c.jpg", 10);
        postTwo.LinkToPost(2);
        postTwo.MarkReady("processed/c.jpg", 8);
        db.MediaAssets.AddRange(postOneFirst, postOneSecond, postTwo);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var mediaByPostId = await service.GetMediaByPostIdsAsync([1, 2, 99]);

        // Assert
        Assert.Equal(2, mediaByPostId.Count);
        Assert.Equal(2, mediaByPostId[1].Count);
        Assert.Single(mediaByPostId[2]);
        Assert.False(mediaByPostId.ContainsKey(99));
    }

    [Fact]
    public async Task GetMediaByPostIdsAsync_ReturnsEmptyDictionary_WhenNoPostIds()
    {
        var db = CreateInMemoryDb();
        var service = CreateMediaService(db, new FakeMediaStorage());

        var mediaByPostId = await service.GetMediaByPostIdsAsync([]);

        Assert.Empty(mediaByPostId);
    }

    [Fact]
    public async Task DeleteBlobStorageForPostsAsync_RemovesMediaForAllPostsInOneBatch()
    {
        // Arrange
        var db = CreateInMemoryDb();
        var storage = new FakeMediaStorage();
        var service = CreateMediaService(db, storage);
        var user = new User("tester@example.com", "hash", "tester");
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var postOneMedia = MediaAsset.CreatePendingUpload(user.Id, MediaIntendedContext.Post, MediaKind.Image, "image/jpeg", "one.jpg", "raw/post/one/original.jpg", 100);
        postOneMedia.LinkToPost(1);
        postOneMedia.MarkReady("raw/post/one/processed.jpg", 80);
        var postTwoMedia = MediaAsset.CreatePendingUpload(user.Id, MediaIntendedContext.Post, MediaKind.Image, "image/jpeg", "two.jpg", "raw/post/two/original.jpg", 100);
        postTwoMedia.LinkToPost(2);
        postTwoMedia.MarkReady("raw/post/two/processed.jpg", 80);
        db.MediaAssets.AddRange(postOneMedia, postTwoMedia);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        storage.SeedObject("raw/post/one/original.jpg");
        storage.SeedObject("raw/post/one/processed.jpg");
        storage.SeedObject("raw/post/two/original.jpg");
        storage.SeedObject("raw/post/two/processed.jpg");

        // Act
        await service.DeleteBlobStorageForPostsAsync([1, 2]);

        // Assert — blob deletes run in parallel; order is not guaranteed
        string[] expectedKeys =
        [
            "raw/post/one/original.jpg",
            "raw/post/one/processed.jpg",
            "raw/post/two/original.jpg",
            "raw/post/two/processed.jpg",
        ];
        Assert.Equivalent(expectedKeys, storage.GetDeletedObjectKeys());
    }

    [Fact]
    public async Task GetMediaForCommentAsync_ThrowsWhenMultipleAssetsLinked()
    {
        // Arrange
        var db = CreateInMemoryDb();
        var service = CreateMediaService(db, new FakeMediaStorage());
        var user = new User("dup@example.com", "hash", "dup");
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var first = MediaAsset.CreatePendingUpload(user.Id, MediaIntendedContext.Comment, MediaKind.Image, "image/jpeg", "a.jpg", "raw/a.jpg", 10);
        first.LinkToComment(5);
        first.MarkReady("processed/a.jpg", 8);
        var second = MediaAsset.CreatePendingUpload(user.Id, MediaIntendedContext.Comment, MediaKind.Image, "image/jpeg", "b.jpg", "raw/b.jpg", 10);
        second.LinkToComment(5);
        second.MarkReady("processed/b.jpg", 8);
        db.MediaAssets.AddRange(first, second);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

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
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var asset = MediaAsset.CreatePendingUpload(
            user.Id,
            MediaIntendedContext.Comment,
            MediaKind.Image,
            "image/jpeg",
            "comment.jpg",
            "raw/comment/pending.jpg",
            100);
        db.MediaAssets.Add(asset);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

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
        var nicknameCache = DomainServiceTestFactory.CreateNicknameCacheService(userRepository, new FakeDistributedCache());
        var userService = new UserService(
            userRepository,
            db,
            new Lazy<PostService>(() => null!),
            new Lazy<CommentService>(() => null!),
            new NoOpMediaClient(),
            new Lazy<ChatMessageService>(() => null!),
            new Lazy<ChatRoomService>(() => null!),
            new Lazy<GroupMembershipService>(() => null!),
            new Lazy<MapPinService>(() => null!),
            new Lazy<LocationSessionService>(() => null!),
            http,
            nicknameCache,
            new NoOpEventPublisher());

        var groupBoardAccess = new GroupBoardAccessService(
            new FakeGroupBoardRepository(),
            new Lazy<GroupService>(() => null!),
            new GroupMembershipService(
                new FakeGroupMemberRepository(),
                new Lazy<GroupService>(() => null!),
                userService,
                http),
            http);

        return new MediaService(
            new MediaAssetRepository(db),
            CreateMediaStorageProvider(storage),
            new MediaLimitPolicy(mediaOptions),
            userService,
            new Lazy<ChatMessageService>(() => null!),
            new Lazy<PostService>(() => null!),
            new Lazy<CommentService>(() => null!),
            groupBoardAccess,
            workQueue,
            mediaOptions,
            http);
    }

    private static ServiceProvider CreateMediaStorageProvider(FakeMediaStorage storage) =>
        new ServiceCollection()
            .AddSingleton<IMediaStorage>(storage)
            .BuildServiceProvider();

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
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
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
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
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
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        if (seedBlob) storage.SeedObject(objectKey);

        return asset;
    }

    private static async Task SeedPostAndCommentMediaAsync(AppDbContext db, FakeMediaStorage storage)
    {
        var user = new User("tester@example.com", "hash", "tester");
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

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
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        storage.SeedObject("raw/post/original.jpg");
        storage.SeedObject("raw/post/processed.jpg");
        storage.SeedObject("raw/comment/original.jpg");
        storage.SeedObject("raw/comment/processed.jpg");
    }
}
