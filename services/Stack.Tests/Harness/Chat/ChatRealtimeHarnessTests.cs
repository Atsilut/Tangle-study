using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Chat.Dto;
using Chat.Events;
using Chat.Realtime;
using Group.Entities;
using Microsoft.AspNetCore.SignalR.Client;
using Stack.Tests.Infrastructure;
using StackExchange.Redis;
using Tangle.TestSupport.Auth;
using Tangle.TestSupport.Harness;
using Tangle.TestSupport.Integration;
using Users.Dto;

namespace Stack.Tests.Harness.Chat;

[Collection(HarnessTestCollection.Name)]
[Trait(HarnessTraits.Category, HarnessTraits.Harness)]
[Trait(HarnessTraits.HarnessModule, HarnessTraits.Chat)]
public sealed class ChatRealtimeHarnessTests : HarnessTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task PostMessage_PushesMessageCreated_ToJoinedHubClient()
    {
        const string testMethodName = nameof(PostMessage_PushesMessageCreated_ToJoinedHubClient);

        var userA = await HarnessAuthHelpers.CreateUserForTestAsync(Client, UniqueTestPrefix(testMethodName), index: 1);
        var userB = await HarnessAuthHelpers.CreateUserForTestAsync(Client, UniqueTestPrefix(testMethodName), index: 2);
        await SocialHarnessHelpers.AcceptFriendshipAsync(Client, userA, userB);
        var room = await ChatHarnessHelpers.GetOrCreateDirectRoomAsync(Client, userA, userB.Id);

        await HarnessAuthHelpers.LoginAsAsync(Client, userA);
        var hubConnection = await HarnessRealtimeTestHelpers.ConnectAndJoinAsync(Client, "hubs/chat", "JoinRoom", room.Id);
        var waitForMessage = HarnessRealtimeTestHelpers.WaitForHubEventAsync<ChatMessageGetResponseDto>(
            hubConnection, ChatHub.MessageCreatedEvent);

        var createRes = await ChatHarnessHelpers.PostMessageAsync(Client, room.Id, "Realtime hello");

        await IntegrationAssertions.AssertStatusAsync(createRes, HttpStatusCode.Created);
        var pushed = await waitForMessage;
        Assert.Equal("Realtime hello", pushed.Body);
        Assert.Equal(room.Id, pushed.ChatRoomId);
        Assert.Equal(userA.Id, pushed.SenderUserId);

        await hubConnection.DisposeAsync();
    }

    [Fact]
    public async Task PostMessage_PushesMessageCreated_ToOtherJoinedParticipant()
    {
        const string testMethodName = nameof(PostMessage_PushesMessageCreated_ToOtherJoinedParticipant);

        var userA = await HarnessAuthHelpers.CreateUserForTestAsync(Client, UniqueTestPrefix(testMethodName), index: 1);
        var userB = await HarnessAuthHelpers.CreateUserForTestAsync(Client, UniqueTestPrefix(testMethodName), index: 2);
        await SocialHarnessHelpers.AcceptFriendshipAsync(Client, userA, userB);
        var room = await ChatHarnessHelpers.GetOrCreateDirectRoomAsync(Client, userA, userB.Id);

        await HarnessAuthHelpers.LoginAsAsync(Client, userB);
        var hubB = await HarnessRealtimeTestHelpers.ConnectAndJoinAsync(Client, "hubs/chat", "JoinRoom", room.Id);
        var waitForMessage = HarnessRealtimeTestHelpers.WaitForHubEventAsync<ChatMessageGetResponseDto>(
            hubB, ChatHub.MessageCreatedEvent);

        await HarnessAuthHelpers.LoginAsAsync(Client, userA);
        var createRes = await ChatHarnessHelpers.PostMessageAsync(Client, room.Id, "Hello from A");

        await IntegrationAssertions.AssertStatusAsync(createRes, HttpStatusCode.Created);
        var pushed = await waitForMessage;
        Assert.Equal("Hello from A", pushed.Body);
        Assert.Equal(room.Id, pushed.ChatRoomId);
        Assert.Equal(userA.Id, pushed.SenderUserId);

        await hubB.DisposeAsync();
    }

    [Fact]
    public async Task JoinRoom_ThrowsHubException_WhenCallerIsNotParticipant()
    {
        const string testMethodName = nameof(JoinRoom_ThrowsHubException_WhenCallerIsNotParticipant);

        var userA = await HarnessAuthHelpers.CreateUserForTestAsync(Client, UniqueTestPrefix(testMethodName), index: 1);
        var userB = await HarnessAuthHelpers.CreateUserForTestAsync(Client, UniqueTestPrefix(testMethodName), index: 2);
        var stranger = await HarnessAuthHelpers.CreateUserForTestAsync(Client, UniqueTestPrefix(testMethodName), index: 3);
        await SocialHarnessHelpers.AcceptFriendshipAsync(Client, userA, userB);
        var room = await ChatHarnessHelpers.GetOrCreateDirectRoomAsync(Client, userA, userB.Id);

        await HarnessAuthHelpers.LoginAsAsync(Client, stranger);
        var hubConnection = ChatHarnessHelpers.BuildHubConnection(Client);
        await hubConnection.StartAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<Microsoft.AspNetCore.SignalR.HubException>(() =>
            hubConnection.InvokeAsync("JoinRoom", room.Id, TestContext.Current.CancellationToken));

        await hubConnection.DisposeAsync();
    }

    [Fact]
    public async Task PostMessage_DoesNotPushToClient_WhenClientNotJoined()
    {
        const string testMethodName = nameof(PostMessage_DoesNotPushToClient_WhenClientNotJoined);

        var userA = await HarnessAuthHelpers.CreateUserForTestAsync(Client, UniqueTestPrefix(testMethodName), index: 1);
        var userB = await HarnessAuthHelpers.CreateUserForTestAsync(Client, UniqueTestPrefix(testMethodName), index: 2);
        await SocialHarnessHelpers.AcceptFriendshipAsync(Client, userA, userB);
        await HarnessAuthHelpers.LoginAsAsync(Client, userA);
        var room = await ChatHarnessHelpers.GetOrCreateDirectRoomAsync(Client, userA, userB.Id);

        var hubConnection = ChatHarnessHelpers.BuildHubConnection(Client);
        var received = new TaskCompletionSource<ChatMessageGetResponseDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        hubConnection.On<ChatMessageGetResponseDto>(ChatHub.MessageCreatedEvent, dto => received.TrySetResult(dto));
        await hubConnection.StartAsync(TestContext.Current.CancellationToken);

        await ChatHarnessHelpers.PostMessageAsync(Client, room.Id, "Should not arrive");

        await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
        Assert.False(received.Task.IsCompleted, "MessageCreated should not be received when client has not joined the room group");

        await hubConnection.DisposeAsync();
    }

    [Fact]
    public async Task PostMessage_EnqueuesChatMessageCreatedJob_ToRedisStream()
    {
        const string testMethodName = nameof(PostMessage_EnqueuesChatMessageCreatedJob_ToRedisStream);

        var userA = await HarnessAuthHelpers.CreateUserForTestAsync(Client, UniqueTestPrefix(testMethodName), index: 1);
        var userB = await HarnessAuthHelpers.CreateUserForTestAsync(Client, UniqueTestPrefix(testMethodName), index: 2);
        await SocialHarnessHelpers.AcceptFriendshipAsync(Client, userA, userB);
        await HarnessAuthHelpers.LoginAsAsync(Client, userA);
        var room = await ChatHarnessHelpers.GetOrCreateDirectRoomAsync(Client, userA, userB.Id);
        var (database, streamKey, lengthBefore) = await GetStreamLengthBeforePostAsync();

        var createRes = await ChatHarnessHelpers.PostMessageAsync(Client, room.Id, "Stream enqueue smoke");

        await IntegrationAssertions.AssertStatusAsync(createRes, HttpStatusCode.Created);
        await WaitForStreamLengthAsync(database, streamKey, lengthBefore + 1);
        await database.Multiplexer.CloseAsync();
    }

    [Theory]
    [InlineData(ChatRoomHarnessKind.Direct)]
    [InlineData(ChatRoomHarnessKind.Multi)]
    [InlineData(ChatRoomHarnessKind.PlatformGroup)]
    public async Task PostMessage_EnqueuesChatMessageCreatedJob_ToRedisStream_ForEachRoomKind(ChatRoomHarnessKind kind)
    {
        var prefix = $"Stream_{kind}_{Guid.NewGuid():N}"[..20];

        var roomId = await CreateRoomIdForKindAsync(kind, prefix);
        var (database, streamKey, lengthBefore) = await GetStreamLengthBeforePostAsync();

        var createRes = await ChatHarnessHelpers.PostMessageAsync(Client, roomId, $"Stream {kind}");

        await IntegrationAssertions.AssertStatusAsync(createRes, HttpStatusCode.Created);
        await WaitForStreamLengthAsync(database, streamKey, lengthBefore + 1);
        await database.Multiplexer.CloseAsync();
    }

    [Fact]
    public async Task PostMessage_PublishesChatMessageCreatedEvent_ToRedisPubSub()
    {
        const string testMethodName = nameof(PostMessage_PublishesChatMessageCreatedEvent_ToRedisPubSub);
        const string messageBody = "PubSub payload";

        var userA = await HarnessAuthHelpers.CreateUserForTestAsync(Client, UniqueTestPrefix(testMethodName), index: 1);
        var userB = await HarnessAuthHelpers.CreateUserForTestAsync(Client, UniqueTestPrefix(testMethodName), index: 2);
        await SocialHarnessHelpers.AcceptFriendshipAsync(Client, userA, userB);
        await HarnessAuthHelpers.LoginAsAsync(Client, userA);
        var room = await ChatHarnessHelpers.GetOrCreateDirectRoomAsync(Client, userA, userB.Id);

        var multiplexer = await ConnectionMultiplexer.ConnectAsync(HarnessRedisFactory.GetConnectionString());
        var subscriber = multiplexer.GetSubscriber();
        var received = new TaskCompletionSource<ChatMessageCreatedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        await subscriber.SubscribeAsync(RedisChannel.Literal(RedisEventChannels.ChatMessageCreated), (_, message) =>
        {
            if (message.IsNullOrEmpty) return;

            var payload = JsonSerializer.Deserialize<ChatMessageCreatedEvent>(message.ToString(), JsonOptions);
            if (payload?.ChatRoomId == room.Id && payload.Body == messageBody) received.TrySetResult(payload);
        });

        var createRes = await ChatHarnessHelpers.PostMessageAsync(Client, room.Id, messageBody);

        await IntegrationAssertions.AssertStatusAsync(createRes, HttpStatusCode.Created);
        var published = await received.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(room.Id, published.ChatRoomId);
        Assert.Equal(userA.Id, published.SenderUserId);
        Assert.Equal(messageBody, published.Body);
        Assert.True(published.MessageId > 0);

        await subscriber.UnsubscribeAsync(RedisChannel.Literal(RedisEventChannels.ChatMessageCreated));
        await multiplexer.CloseAsync();
    }

    [Fact]
    public async Task ListMessages_ReturnsUpdatedNickname_AfterUsersPatch()
    {
        const string testMethodName = nameof(ListMessages_ReturnsUpdatedNickname_AfterUsersPatch);
        const string updatedNickname = "HarnessUpdatedNick";

        var userA = await HarnessAuthHelpers.CreateUserForTestAsync(Client, UniqueTestPrefix(testMethodName), index: 1);
        var userB = await HarnessAuthHelpers.CreateUserForTestAsync(Client, UniqueTestPrefix(testMethodName), index: 2);
        await SocialHarnessHelpers.AcceptFriendshipAsync(Client, userA, userB);
        var room = await ChatHarnessHelpers.GetOrCreateDirectRoomAsync(Client, userA, userB.Id);
        var createRes = await ChatHarnessHelpers.PostMessageAsync(Client, room.Id, "Nickname refresh harness");
        await IntegrationAssertions.AssertStatusAsync(createRes, HttpStatusCode.Created);

        await HarnessAuthHelpers.LoginAsAsync(Client, userA);
        var patchRes = await Client.PatchAsJsonAsync(
            "/api/users",
            new UserPatchRequestDto { Id = userA.Id, Nickname = updatedNickname },
            TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(patchRes, HttpStatusCode.OK);

        await HarnessAuthHelpers.LoginAsAsync(Client, userB);
        var listRes = await Client.GetAsync($"{ChatHarnessHelpers.ChatRoomsBase}/{room.Id}/messages", TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(listRes, HttpStatusCode.OK);
        var messages = await listRes.Content.ReadFromJsonAsync<List<ChatMessageGetResponseDto>>(TestContext.Current.CancellationToken);
        Assert.NotNull(messages);
        Assert.Contains(messages, m => m.SenderUserId == userA.Id && m.SenderNickname == updatedNickname);
    }

    private static async Task<(IDatabase Database, RedisKey StreamKey, long LengthBefore)> GetStreamLengthBeforePostAsync()
    {
        var multiplexer = await ConnectionMultiplexer.ConnectAsync(HarnessRedisFactory.GetConnectionString());
        var streamKey = (RedisKey)ChatHarnessHelpers.GetWorkQueueStreamKey();
        var database = multiplexer.GetDatabase();
        var lengthBefore = await database.StreamLengthAsync(streamKey);
        return (database, streamKey, lengthBefore);
    }

    private static Task<long> WaitForStreamLengthAsync(IDatabase database, RedisKey streamKey, long expectedLength) =>
        IntegrationTestPolling.PollUntilAsync(
            async ct => await database.StreamLengthAsync(streamKey),
            length => length >= expectedLength,
            timeout: TimeSpan.FromSeconds(10),
            delay: TimeSpan.FromMilliseconds(200),
            cancellationToken: TestContext.Current.CancellationToken);

    private async Task<long> CreateRoomIdForKindAsync(ChatRoomHarnessKind kind, string prefix)
    {
        var user1 = await HarnessAuthHelpers.CreateUserForTestAsync(Client, prefix, index: 1);
        var user2 = await HarnessAuthHelpers.CreateUserForTestAsync(Client, prefix, index: 2);

        return kind switch
        {
            ChatRoomHarnessKind.Direct => (await CreateDirectRoomAsync(user1, user2)).Id,
            ChatRoomHarnessKind.Multi => (await ChatHarnessHelpers.CreateMultiRoomAsync(Client, user1, [user2.Id])).Id,
            ChatRoomHarnessKind.PlatformGroup => await CreatePlatformGroupRoomIdAsync(user1, user2),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    private async Task<ChatRoomGetResponseDto> CreateDirectRoomAsync(UserGetResponseDto user1, UserGetResponseDto user2)
    {
        await SocialHarnessHelpers.AcceptFriendshipAsync(Client, user1, user2);
        return await ChatHarnessHelpers.GetOrCreateDirectRoomAsync(Client, user1, user2.Id);
    }

    private async Task<long> CreatePlatformGroupRoomIdAsync(UserGetResponseDto owner, UserGetResponseDto member)
    {
        var group = await GroupHarnessHelpers.CreateGroupAsync(
            Client,
            owner,
            GroupVisibility.Public,
            GroupJoinPolicy.Open);
        var invitation = await GroupHarnessHelpers.InviteUserAsync(Client, owner, group.Id, member.Id);
        await GroupHarnessHelpers.AcceptInvitationAsync(Client, member, invitation.Id);
        return (await ChatHarnessHelpers.CreatePlatformGroupChatRoomAsync(Client, owner, group.Id, [member.Id])).Id;
    }
}

public enum ChatRoomHarnessKind
{
    Direct,
    Multi,
    PlatformGroup,
}
