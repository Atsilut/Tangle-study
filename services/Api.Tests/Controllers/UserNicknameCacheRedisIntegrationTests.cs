using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Domain.Users.Dto;
using Api.Global.Events;
using Api.Tests.Infrastructure;
using StackExchange.Redis;

namespace Api.Tests.Controllers;

[Collection(RedisRealtimeIntegrationTestCollection.Name)]
public sealed class UserNicknameCacheRedisIntegrationTests(
    PostgresTestcontainerFixture postgres,
    RedisTestcontainerFixture redis)
    : IntegrationTestBase(postgres, redisEnabled: true, redisConnectionString: redis.ConnectionString)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task UpdateNickname_PublishesUserNicknameChangedEvent_ToRedisPubSub()
    {
        const string testMethodName = nameof(UpdateNickname_PublishesUserNicknameChangedEvent_ToRedisPubSub);
        const string updatedNickname = "PubSubNick";

        // Arrange
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);

        var multiplexer = await ConnectionMultiplexer.ConnectAsync(redis.ConnectionString);
        var subscriber = multiplexer.GetSubscriber();
        var received = new TaskCompletionSource<UserNicknameChangedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        await subscriber.SubscribeAsync(RedisChannel.Literal(RedisEventChannels.UserNicknameChanged), (_, message) =>
        {
            if (message.IsNullOrEmpty) return;

            var payload = JsonSerializer.Deserialize<UserNicknameChangedEvent>(message.ToString(), JsonOptions);
            if (payload?.UserId == user.Id) received.TrySetResult(payload);
        });

        // Act
        var patch = await Client.PatchAsJsonAsync(
            "/api/users",
            new UserPatchRequestDto { Id = user.Id, Nickname = updatedNickname },
            TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(patch, HttpStatusCode.OK);
        var published = await received.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(user.Id, published.UserId);
        Assert.Equal(updatedNickname, published.Nickname);
        Assert.False(published.IsDeleted);

        await subscriber.UnsubscribeAsync(RedisChannel.Literal(RedisEventChannels.UserNicknameChanged));
        await multiplexer.CloseAsync();
    }
}
