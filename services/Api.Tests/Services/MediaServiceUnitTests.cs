using Api.Domain.Media;
using Api.Domain.Media.Domain;
using Api.Domain.Media.Repository;
using Api.Domain.Media.Service;
using Api.Domain.Users.Domain;
using Api.Domain.Users.Service;
using Api.Global.Config;
using Api.Global.Db;
using Api.Global.Events;
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

    private static MediaService CreateMediaService(AppDbContext db, FakeMediaStorage storage)
    {
        var mediaOptions = Options.Create(new MediaOptions { Enabled = true });
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
            mediaOptions,
            http);
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
