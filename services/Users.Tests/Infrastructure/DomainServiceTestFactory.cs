using Users.Service;
using Users.Config;
using Users.Db;
using Users.Infrastructure;
using Users.Tests.Repositories;
using Tangle.AspNetCore.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Users.Tests.Infrastructure;

internal static class DomainServiceTestFactory
{
    internal sealed record Graph(
        UserService UserService,
        FakeUserRepository UserRepository,
        FakeCommunityClient CommunityClient,
        FakeMediaClient MediaClient,
        FakeChatClient ChatClient,
        FakeLocationClient LocationClient,
        FakeGroupClient GroupClient,
        FakeSocialClient SocialClient);

    public static Graph Create(FakeHttpContextAccessor? httpContextAccessor = null)
    {
        var userRepository = new FakeUserRepository();
        var http = httpContextAccessor ?? new FakeHttpContextAccessor("1");
        var currentUser = new CurrentUserAccessor(http);
        var distributedCache = new FakeDistributedCache();
        var nicknameCacheService = CreateNicknameCacheService(userRepository, distributedCache);
        var eventPublisher = new NullEventPublisher();
        var locationClient = new FakeLocationClient();
        var communityClient = new FakeCommunityClient();
        var groupClient = new FakeGroupClient();
        var socialClient = new FakeSocialClient();
        var db = new UsersDbContext(new DbContextOptionsBuilder<UsersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        var mediaClient = new FakeMediaClient();
        var chatClient = new FakeChatClient();

        var userService = new UserService(
            userRepository,
            db,
            communityClient,
            mediaClient,
            chatClient,
            locationClient,
            groupClient,
            socialClient,
            currentUser,
            nicknameCacheService,
            eventPublisher);

        return new Graph(
            userService,
            userRepository,
            communityClient,
            mediaClient,
            chatClient,
            locationClient,
            groupClient,
            socialClient);
    }

    internal static NicknameCacheService CreateNicknameCacheService(
        FakeUserRepository userRepository,
        FakeDistributedCache cache)
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.Configure<RedisOptions>(options => options.InstanceName = "tangle:");
        var serviceProvider = services.BuildServiceProvider();
        return new NicknameCacheService(
            userRepository,
            cache,
            serviceProvider,
            serviceProvider.GetRequiredService<IOptions<RedisOptions>>());
    }

    private sealed class NullEventPublisher : IEventPublisher
    {
        public Task PublishAsync<TPayload>(string channel, TPayload payload, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
