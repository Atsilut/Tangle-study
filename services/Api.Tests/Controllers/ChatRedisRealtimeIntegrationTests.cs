using System.Net;
using System.Text.Json;
using Api.Domain.Chat.Dto;
using Api.Domain.Chat.Realtime;
using Api.Domain.Users.Dto;
using Api.Global.Config;
using Api.Global.Events;
using Api.Tests.Infrastructure;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Api.Tests.Controllers;

[Collection(RedisRealtimeIntegrationTestCollection.Name)]
public sealed class ChatHubRealtimeIntegrationTests(
    PostgresTestcontainerFixture postgres,
    RedisTestcontainerFixture redis)
    : ChatIntegrationTestBase(postgres, redisEnabled: true, redisConnectionString: redis.ConnectionString)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // --- SignalR hub ---

    [Fact]
    public async Task PostMessage_PushesMessageCreated_ToJoinedHubClient()
    {
        const string testMethodName = nameof(PostMessage_PushesMessageCreated_ToJoinedHubClient);

        // Arrange
        var userA = await CreateUserForTest(testMethodName, 1);
        var userB = await CreateUserForTest(testMethodName, 2);
        await AcceptFriendshipAsync(userA, userB);
        await LoginAs(userA);
        var token = Client.DefaultRequestHeaders.Authorization!.Parameter!;
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);

        var received = new TaskCompletionSource<ChatMessageGetResponseDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var hubConnection = ChatRealtimeTestHelpers.BuildHubConnection(Factory, Client, token);
        hubConnection.On<ChatMessageGetResponseDto>(ChatHub.MessageCreatedEvent, dto => received.TrySetResult(dto));
        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("JoinRoom", room.Id);

        // Act
        var createRes = await PostMessageAsync(room.Id, "Realtime hello");

        // Assert
        await IntegrationAssertions.AssertStatusAsync(createRes, HttpStatusCode.Created);
        var pushed = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("Realtime hello", pushed.Body);
        Assert.Equal(room.Id, pushed.ChatRoomId);
        Assert.Equal(userA.Id, pushed.SenderUserId);

        await hubConnection.DisposeAsync();
    }

    [Fact]
    public async Task PostMessage_PushesMessageCreated_ToOtherJoinedParticipant()
    {
        const string testMethodName = nameof(PostMessage_PushesMessageCreated_ToOtherJoinedParticipant);

        // Arrange
        var userA = await CreateUserForTest(testMethodName, 1);
        var userB = await CreateUserForTest(testMethodName, 2);
        await AcceptFriendshipAsync(userA, userB);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);

        await LoginAs(userB);
        var tokenB = Client.DefaultRequestHeaders.Authorization!.Parameter!;
        var receivedByB = new TaskCompletionSource<ChatMessageGetResponseDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var hubB = ChatRealtimeTestHelpers.BuildHubConnection(Factory, Client, tokenB);
        hubB.On<ChatMessageGetResponseDto>(ChatHub.MessageCreatedEvent, dto => receivedByB.TrySetResult(dto));
        await hubB.StartAsync();
        await hubB.InvokeAsync("JoinRoom", room.Id);

        // Act
        await LoginAs(userA);
        var createRes = await PostMessageAsync(room.Id, "Hello from A");

        // Assert
        await IntegrationAssertions.AssertStatusAsync(createRes, HttpStatusCode.Created);
        var pushed = await receivedByB.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("Hello from A", pushed.Body);
        Assert.Equal(room.Id, pushed.ChatRoomId);
        Assert.Equal(userA.Id, pushed.SenderUserId);

        await hubB.DisposeAsync();
    }

    [Fact]
    public async Task JoinRoom_ThrowsHubException_WhenCallerIsNotParticipant()
    {
        const string testMethodName = nameof(JoinRoom_ThrowsHubException_WhenCallerIsNotParticipant);

        // Arrange
        var userA = await CreateUserForTest(testMethodName, 1);
        var userB = await CreateUserForTest(testMethodName, 2);
        var stranger = await CreateUserForTest(testMethodName, 3);
        await AcceptFriendshipAsync(userA, userB);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);
        await LoginAs(stranger);
        var token = Client.DefaultRequestHeaders.Authorization!.Parameter!;
        var hubConnection = ChatRealtimeTestHelpers.BuildHubConnection(Factory, Client, token);
        await hubConnection.StartAsync();

        // Act & Assert
        await Assert.ThrowsAsync<HubException>(() =>
            hubConnection.InvokeAsync("JoinRoom", room.Id));

        await hubConnection.DisposeAsync();
    }

    [Fact]
    public async Task PostMessage_DoesNotPushToClient_WhenClientNotJoined()
    {
        const string testMethodName = nameof(PostMessage_DoesNotPushToClient_WhenClientNotJoined);

        // Arrange
        var userA = await CreateUserForTest(testMethodName, 1);
        var userB = await CreateUserForTest(testMethodName, 2);
        await AcceptFriendshipAsync(userA, userB);
        await LoginAs(userA);
        var token = Client.DefaultRequestHeaders.Authorization!.Parameter!;
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);

        var hubConnection = ChatRealtimeTestHelpers.BuildHubConnection(Factory, Client, token);
        var received = new TaskCompletionSource<ChatMessageGetResponseDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        hubConnection.On<ChatMessageGetResponseDto>(ChatHub.MessageCreatedEvent, dto => received.TrySetResult(dto));
        await hubConnection.StartAsync();

        // Act
        await PostMessageAsync(room.Id, "Should not arrive");

        // Assert
        await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.False(received.Task.IsCompleted, "MessageCreated should not be received when client has not joined the room group");

        await hubConnection.DisposeAsync();
    }

    // --- Redis stream ---

    [Fact]
    public async Task PostMessage_EnqueuesChatMessageCreatedJob_ToRedisStream()
    {
        const string testMethodName = nameof(PostMessage_EnqueuesChatMessageCreatedJob_ToRedisStream);

        // Arrange
        var userA = await CreateUserForTest(testMethodName, 1);
        var userB = await CreateUserForTest(testMethodName, 2);
        await AcceptFriendshipAsync(userA, userB);
        await LoginAs(userA);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);
        var (database, streamKey, lengthBefore) = await GetStreamLengthBeforePostAsync();

        // Act
        var createRes = await PostMessageAsync(room.Id, "Stream enqueue smoke");

        // Assert
        await IntegrationAssertions.AssertStatusAsync(createRes, HttpStatusCode.Created);
        var lengthAfter = await database.StreamLengthAsync(streamKey);
        Assert.Equal(lengthBefore + 1, lengthAfter);
        await database.Multiplexer.CloseAsync();
    }

    [Theory]
    [InlineData(ChatRoomMatrixKind.Direct)]
    [InlineData(ChatRoomMatrixKind.Multi)]
    [InlineData(ChatRoomMatrixKind.PlatformGroup)]
    public async Task PostMessage_EnqueuesChatMessageCreatedJob_ToRedisStream_ForEachRoomKind(ChatRoomMatrixKind kind)
    {
        var prefix = $"Stream_{kind}_{Guid.NewGuid():N}"[..20];

        // Arrange
        var roomId = await CreateRoomIdForKindAsync(kind, prefix);
        var (database, streamKey, lengthBefore) = await GetStreamLengthBeforePostAsync();

        // Act
        var createRes = await PostMessageAsync(roomId, $"Stream {kind}");

        // Assert
        await IntegrationAssertions.AssertStatusAsync(createRes, HttpStatusCode.Created);
        var lengthAfter = await database.StreamLengthAsync(streamKey);
        Assert.Equal(lengthBefore + 1, lengthAfter);
        await database.Multiplexer.CloseAsync();
    }

    // --- Redis pub/sub ---

    [Fact]
    public async Task PostMessage_PublishesChatMessageCreatedEvent_ToRedisPubSub()
    {
        const string testMethodName = nameof(PostMessage_PublishesChatMessageCreatedEvent_ToRedisPubSub);
        const string messageBody = "PubSub payload";

        // Arrange
        var userA = await CreateUserForTest(testMethodName, 1);
        var userB = await CreateUserForTest(testMethodName, 2);
        await AcceptFriendshipAsync(userA, userB);
        await LoginAs(userA);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);

        var multiplexer = await ConnectionMultiplexer.ConnectAsync(redis.ConnectionString);
        var subscriber = multiplexer.GetSubscriber();
        var received = new TaskCompletionSource<ChatMessageCreatedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        await subscriber.SubscribeAsync(RedisChannel.Literal(RedisEventChannels.ChatMessageCreated), (_, message) =>
        {
            if (message.IsNullOrEmpty)
                return;

            var payload = JsonSerializer.Deserialize<ChatMessageCreatedEvent>(message.ToString(), JsonOptions);
            if (payload?.ChatRoomId == room.Id && payload.Body == messageBody)
                received.TrySetResult(payload);
        });

        // Act
        var createRes = await PostMessageAsync(room.Id, messageBody);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(createRes, HttpStatusCode.Created);
        var published = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(room.Id, published.ChatRoomId);
        Assert.Equal(userA.Id, published.SenderUserId);
        Assert.Equal(messageBody, published.Body);
        Assert.True(published.MessageId > 0);

        await subscriber.UnsubscribeAsync(RedisChannel.Literal(RedisEventChannels.ChatMessageCreated));
        await multiplexer.CloseAsync();
    }

    private async Task<(IDatabase Database, RedisKey StreamKey, long LengthBefore)> GetStreamLengthBeforePostAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var redisOptions = scope.ServiceProvider.GetRequiredService<IOptions<RedisOptions>>().Value;
        Assert.True(redisOptions.Enabled);
        var multiplexer = await ConnectionMultiplexer.ConnectAsync(redis.ConnectionString);
        var streamKey = ChatRealtimeTestHelpers.GetWorkQueueStreamKey(redisOptions);
        var database = multiplexer.GetDatabase();
        var lengthBefore = await database.StreamLengthAsync(streamKey);
        return (database, streamKey, lengthBefore);
    }

    private async Task<long> CreateRoomIdForKindAsync(ChatRoomMatrixKind kind, string prefix)
    {
        var user1 = await CreateUserForTest(prefix, 1);
        var user2 = await CreateUserForTest(prefix, 2);

        return kind switch
        {
            ChatRoomMatrixKind.Direct => await CreateDirectRoomIdAsync(user1, user2),
            ChatRoomMatrixKind.Multi => (await CreateMultiRoomAsync(user1, [user2.Id])).Id,
            ChatRoomMatrixKind.PlatformGroup => await CreatePlatformGroupRoomIdAsync(user1, user2),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    private async Task<long> CreateDirectRoomIdAsync(UserGetResponseDto user1, UserGetResponseDto user2)
    {
        await AcceptFriendshipAsync(user1, user2);
        return (await GetOrCreateDirectRoomAsync(user1, user2.Id)).Id;
    }

    private async Task<long> CreatePlatformGroupRoomIdAsync(UserGetResponseDto owner, UserGetResponseDto member)
    {
        var group = await CreateGroupWithMemberAsync(owner, member);
        return (await CreatePlatformGroupChatRoomAsync(owner, group.Id, [member.Id])).Id;
    }
}
