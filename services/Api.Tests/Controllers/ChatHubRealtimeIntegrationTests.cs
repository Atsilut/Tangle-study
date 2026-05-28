using System.Net;
using System.Net.Http.Json;
using Api.Domain.Chat.Dto;
using Api.Domain.Chat.Realtime;
using Api.Global.Config;
using Api.Global.Queue;
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
    private HubConnection BuildHubConnection(string token) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(Client.BaseAddress!, "hubs/chat"), options =>
            {
                options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();

    [Fact]
    public async Task PostMessage_PushesMessageCreated_ToJoinedHubClient()
    {
        var testMethodName = nameof(PostMessage_PushesMessageCreated_ToJoinedHubClient);

        // Arrange
        var userA = await CreateUserForTest(testMethodName, 1);
        var userB = await CreateUserForTest(testMethodName, 2);
        await AcceptFriendshipAsync(userA, userB);
        await LoginAs(userA);
        var token = Client.DefaultRequestHeaders.Authorization!.Parameter!;
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);

        var received = new TaskCompletionSource<ChatMessageGetResponseDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var hubConnection = BuildHubConnection(token);
        hubConnection.On<ChatMessageGetResponseDto>(ChatHub.MessageCreatedEvent, dto => received.TrySetResult(dto));
        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("JoinRoom", room.Id);

        // Act
        var createRes = await Client.PostAsJsonAsync(
            $"{ChatRoomsBase}/{room.Id}/messages",
            new ChatMessageCreateRequestDto { Body = "Realtime hello" });

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
        var testMethodName = nameof(PostMessage_PushesMessageCreated_ToOtherJoinedParticipant);

        // Arrange
        var userA = await CreateUserForTest(testMethodName, 1);
        var userB = await CreateUserForTest(testMethodName, 2);
        await AcceptFriendshipAsync(userA, userB);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);

        await LoginAs(userB);
        var tokenB = Client.DefaultRequestHeaders.Authorization!.Parameter!;
        var receivedByB = new TaskCompletionSource<ChatMessageGetResponseDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var hubB = BuildHubConnection(tokenB);
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
        var testMethodName = nameof(JoinRoom_ThrowsHubException_WhenCallerIsNotParticipant);

        // Arrange
        var userA = await CreateUserForTest(testMethodName, 1);
        var userB = await CreateUserForTest(testMethodName, 2);
        var stranger = await CreateUserForTest(testMethodName, 3);
        await AcceptFriendshipAsync(userA, userB);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);
        await LoginAs(stranger);
        var token = Client.DefaultRequestHeaders.Authorization!.Parameter!;
        var hubConnection = BuildHubConnection(token);
        await hubConnection.StartAsync();

        // Act & Assert
        await Assert.ThrowsAsync<HubException>(() =>
            hubConnection.InvokeAsync("JoinRoom", room.Id));

        await hubConnection.DisposeAsync();
    }

    [Fact]
    public async Task PostMessage_DoesNotPushToClient_WhenClientNotJoined()
    {
        var testMethodName = nameof(PostMessage_DoesNotPushToClient_WhenClientNotJoined);

        // Arrange
        var userA = await CreateUserForTest(testMethodName, 1);
        var userB = await CreateUserForTest(testMethodName, 2);
        await AcceptFriendshipAsync(userA, userB);
        await LoginAs(userA);
        var token = Client.DefaultRequestHeaders.Authorization!.Parameter!;
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);

        var hubConnection = BuildHubConnection(token);
        var received = new TaskCompletionSource<ChatMessageGetResponseDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        hubConnection.On<ChatMessageGetResponseDto>(ChatHub.MessageCreatedEvent, dto => received.TrySetResult(dto));
        await hubConnection.StartAsync();

        // Act & Assert
        await PostMessageAsync(room.Id, "Should not arrive");
        await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.False(received.Task.IsCompleted, "MessageCreated should not be received when client has not joined the room group");

        await hubConnection.DisposeAsync();
    }

    [Fact]
    public async Task PostMessage_EnqueuesChatMessageCreatedJob_ToRedisStream()
    {
        var testMethodName = nameof(PostMessage_EnqueuesChatMessageCreatedJob_ToRedisStream);

        // Arrange
        var userA = await CreateUserForTest(testMethodName, 1);
        var userB = await CreateUserForTest(testMethodName, 2);
        await AcceptFriendshipAsync(userA, userB);
        await LoginAs(userA);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);

        await using var scope = Factory.Services.CreateAsyncScope();
        var redisOptions = scope.ServiceProvider.GetRequiredService<IOptions<RedisOptions>>().Value;
        Assert.True(redisOptions.Enabled);
        var multiplexer = await ConnectionMultiplexer.ConnectAsync(redis.ConnectionString);
        var streamKey = redisOptions.WorkQueueStreamPrefix.EndsWith(':')
            ? $"{redisOptions.WorkQueueStreamPrefix}{WorkQueueStreams.ChatMessageCreated}"
            : $"{redisOptions.WorkQueueStreamPrefix}:{WorkQueueStreams.ChatMessageCreated}";
        var database = multiplexer.GetDatabase();
        var lengthBefore = await database.StreamLengthAsync(streamKey);

        // Act
        var createRes = await PostMessageAsync(room.Id, "Stream enqueue smoke");

        // Assert
        await IntegrationAssertions.AssertStatusAsync(createRes, HttpStatusCode.Created);
        var lengthAfter = await database.StreamLengthAsync(streamKey);
        Assert.Equal(lengthBefore + 1, lengthAfter);
        await multiplexer.CloseAsync();
    }
}
