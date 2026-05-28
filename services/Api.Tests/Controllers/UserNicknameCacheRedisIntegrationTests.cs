using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Domain.Chat.Dto;
using Api.Domain.Users.Dto;
using Api.Global.Events;
using Api.Tests.Infrastructure;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Api.Tests.Controllers;

[Collection(RedisRealtimeIntegrationTestCollection.Name)]
public sealed class UserNicknameCacheRedisIntegrationTests(
    PostgresTestcontainerFixture postgres,
    RedisTestcontainerFixture redis)
    : ChatIntegrationTestBase(postgres, redisEnabled: true, redisConnectionString: redis.ConnectionString)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task UpdateNickname_InvalidatesDistributedCache_AndReturnsNewNicknameOnChatMessage()
    {
        var testMethodName = nameof(UpdateNickname_InvalidatesDistributedCache_AndReturnsNewNicknameOnChatMessage);
        const string updatedNickname = "RedisCacheNick";

        // Arrange
        var userA = await CreateUserForTest(testMethodName, 1);
        var userB = await CreateUserForTest(testMethodName, 2);
        await AcceptFriendshipAsync(userA, userB);
        await LoginAs(userA);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);

        var warmCacheRes = await PostMessageAsync(room.Id, "Warm cache");
        await IntegrationAssertions.AssertStatusAsync(warmCacheRes, HttpStatusCode.Created);

        await using var scope = Factory.Services.CreateAsyncScope();
        var cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();
        var nicknameCacheKey = $"users:nickname:{userA.Id}";
        var cachedBeforeUpdate = await cache.GetStringAsync(nicknameCacheKey);
        Assert.False(string.IsNullOrWhiteSpace(cachedBeforeUpdate));
        Assert.Equal(userA.Nickname, cachedBeforeUpdate);

        // Act
        var patch = await Client.PatchAsJsonAsync("/api/users", new UserPatchRequestDto(userA.Id, updatedNickname));

        // Assert
        await IntegrationAssertions.AssertStatusAsync(patch, HttpStatusCode.OK);
        var cachedAfterUpdate = await cache.GetStringAsync(nicknameCacheKey);
        Assert.Null(cachedAfterUpdate);

        await LoginAs(userB);
        var listRes = await Client.GetAsync($"{ChatRoomsBase}/{room.Id}/messages");
        await IntegrationAssertions.AssertStatusAsync(listRes, HttpStatusCode.OK);
        var messages = await listRes.Content.ReadFromJsonAsync<List<ChatMessageGetResponseDto>>();
        Assert.NotNull(messages);
        Assert.Contains(messages, m => m.SenderUserId == userA.Id && m.SenderNickname == updatedNickname);
    }

    [Fact]
    public async Task UpdateNickname_PublishesUserNicknameChangedEvent_ToRedisPubSub()
    {
        var testMethodName = nameof(UpdateNickname_PublishesUserNicknameChangedEvent_ToRedisPubSub);
        const string updatedNickname = "PubSubNick";

        // Arrange
        var user = await CreateUserForTest(testMethodName);
        await LoginAs(user);

        var multiplexer = await ConnectionMultiplexer.ConnectAsync(redis.ConnectionString);
        var subscriber = multiplexer.GetSubscriber();
        var received = new TaskCompletionSource<UserNicknameChangedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        await subscriber.SubscribeAsync(RedisChannel.Literal(RedisEventChannels.UserNicknameChanged), (_, message) =>
        {
            if (message.IsNullOrEmpty)
                return;

            var payload = JsonSerializer.Deserialize<UserNicknameChangedEvent>(message.ToString(), JsonOptions);
            if (payload?.UserId == user.Id)
                received.TrySetResult(payload);
        });

        // Act
        var patch = await Client.PatchAsJsonAsync("/api/users", new UserPatchRequestDto(user.Id, updatedNickname));

        // Assert
        await IntegrationAssertions.AssertStatusAsync(patch, HttpStatusCode.OK);
        var published = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(user.Id, published.UserId);
        Assert.Equal(updatedNickname, published.Nickname);
        Assert.False(published.IsDeleted);

        await subscriber.UnsubscribeAsync(RedisChannel.Literal(RedisEventChannels.UserNicknameChanged));
        await multiplexer.CloseAsync();
    }
}
