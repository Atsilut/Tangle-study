using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Social.Db;
using Social.Friendships.Service;
using Social.Tests.Repositories;
using Social.UserBlocks.Service;

namespace Social.Tests.Infrastructure;

public sealed class SocialServiceTestFactory : IDisposable
{
    public FriendshipService FriendshipService { get; }
    public FriendRequestService FriendRequestService { get; }
    public UserBlockService UserBlockService { get; }
    public FakeFriendshipRepository FriendshipRepository { get; }
    public FakeFriendRequestRepository FriendRequestRepository { get; }
    public FakeUserBlockRepository UserBlockRepository { get; }
    public InMemoryUserClient Monolith { get; }
    public FakeHttpContextAccessor Http { get; }
    public SocialDbContext Db { get; }

    private SocialServiceTestFactory(
        FriendshipService friendshipService,
        FriendRequestService friendRequestService,
        UserBlockService userBlockService,
        FakeFriendshipRepository friendshipRepository,
        FakeFriendRequestRepository friendRequestRepository,
        FakeUserBlockRepository userBlockRepository,
        InMemoryUserClient monolith,
        FakeHttpContextAccessor http,
        SocialDbContext db)
    {
        FriendshipService = friendshipService;
        FriendRequestService = friendRequestService;
        UserBlockService = userBlockService;
        FriendshipRepository = friendshipRepository;
        FriendRequestRepository = friendRequestRepository;
        UserBlockRepository = userBlockRepository;
        Monolith = monolith;
        Http = http;
        Db = db;
    }

    public static SocialServiceTestFactory Create(FakeHttpContextAccessor? http = null)
    {
        http ??= new FakeHttpContextAccessor("1");
        var friendshipRepo = new FakeFriendshipRepository();
        var friendRequestRepo = new FakeFriendRequestRepository();
        var userBlockRepo = new FakeUserBlockRepository();
        var monolith = new InMemoryUserClient();
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

        return new SocialServiceTestFactory(
            friendshipService,
            friendRequestService,
            userBlockService,
            friendshipRepo,
            friendRequestRepo,
            userBlockRepo,
            monolith,
            http,
            db);
    }

    public void SetCaller(long userId)
    {
        Http.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity([new Claim("sub", userId.ToString())], authenticationType: "Test")),
        };
    }

    public long SeedUser(string nickname) => Monolith.SeedUser(nickname);

    public void Dispose() => Db.Dispose();
}
