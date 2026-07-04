using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Social.Db;
using Social.Friendships.Dto;
using Social.Friendships.Service;
using Social.Tests.Infrastructure;
using Social.Tests.Repositories;
using Social.UserBlocks.Service;

namespace Social.Tests.Services;

public sealed class FriendshipServiceUnitTests
{
    [Fact]
    public async Task SendAndAccept_CreatesFriendship()
    {
        var graph = CreateGraph();
        var userA = graph.Monolith.SeedUser("a");
        var userB = graph.Monolith.SeedUser("b");
        graph.Http.HttpContext!.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                [new System.Security.Claims.Claim("sub", userA.ToString())],
                authenticationType: "Test"));

        await graph.FriendRequestService.SendRequestAsync(
            new FriendRequestCreateRequestDto { AddresseeId = userB });

        graph.Http.HttpContext!.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                [new System.Security.Claims.Claim("sub", userB.ToString())],
                authenticationType: "Test"));

        var pending = await graph.FriendRequestService.GetPendingAsync();
        Assert.NotNull(pending);
        await graph.FriendRequestService.AcceptRequestAsync(pending.Single().Id);

        graph.Http.HttpContext!.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                [new System.Security.Claims.Claim("sub", userA.ToString())],
                authenticationType: "Test"));

        var friends = await graph.FriendshipService.GetMyFriendsAsync();
        Assert.NotNull(friends);
        Assert.Contains(friends, f => f.OtherUserId == userB);
    }

    private static (
        FriendshipService FriendshipService,
        FriendRequestService FriendRequestService,
        UserBlockService UserBlockService,
        InMemoryMonolithAccessClient Monolith,
        FakeHttpContextAccessor Http) CreateGraph()
    {
        var friendshipRepo = new FakeFriendshipRepository();
        var friendRequestRepo = new FakeFriendRequestRepository();
        var userBlockRepo = new FakeUserBlockRepository();
        var http = new FakeHttpContextAccessor("1");
        var monolith = new InMemoryMonolithAccessClient();
        var db = new SocialDbContext(new DbContextOptionsBuilder<SocialDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        FriendRequestService friendRequestService = null!;
        var friendshipService = new FriendshipService(friendshipRepo, monolith, http);
        var userBlockService = new UserBlockService(
            userBlockRepo,
            new Lazy<FriendRequestService>(() => friendRequestService),
            monolith,
            http);
        friendRequestService = new FriendRequestService(
            friendRequestRepo,
            friendshipService,
            monolith,
            userBlockService,
            db,
            http,
            NullLogger<FriendRequestService>.Instance);

        return (friendshipService, friendRequestService, userBlockService, monolith, http);
    }
}
