using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Users.Dto;
using Users.Tests.Infrastructure;
using StackExchange.Redis;
using Users.Infrastructure;

namespace Users.Tests.Controllers;

[Collection(UsersIntegrationTestCollection.Name)]
public sealed class UserNicknameCacheRedisIntegrationTests(
    PostgresTestcontainerFixture postgres,
    RedisTestcontainerFixture redis)
    : IntegrationTestBase(postgres, redis)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task UpdateNickname_PublishesUserNicknameChangedEvent_ToRedisPubSub()
    {
        const string testMethodName = nameof(UpdateNickname_PublishesUserNicknameChangedEvent_ToRedisPubSub);
        const string updatedNickname = "PubSubNick";

        // Arrange
        var user = await UsersTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await UsersTestAuthHelpers.LoginAsAsync(Client, user);

        var multiplexer = await ConnectionMultiplexer.ConnectAsync(Redis.ConnectionString);
        try
        {
            var subscriber = multiplexer.GetSubscriber();
            var received = new TaskCompletionSource<UserNicknameChangedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
            await subscriber.SubscribeAsync(RedisChannel.Literal(RedisEventChannels.UserNicknameChanged), (_, message) =>
            {
                if (message.IsNullOrEmpty) return;

                var payload = JsonSerializer.Deserialize<UserNicknameChangedEvent>(message.ToString(), JsonOptions);
                if (payload?.UserId == user.Id) received.TrySetResult(payload);
            });

            try
            {
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
            }
            finally
            {
                await subscriber.UnsubscribeAsync(RedisChannel.Literal(RedisEventChannels.UserNicknameChanged));
            }
        }
        finally
        {
            await multiplexer.CloseAsync();
        }
    }
}
