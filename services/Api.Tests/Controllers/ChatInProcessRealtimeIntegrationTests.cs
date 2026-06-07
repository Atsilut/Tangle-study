using System.Net;
using Api.Domain.Chat.Dto;
using Api.Domain.Chat.Realtime;
using Api.Tests.Infrastructure;
using Microsoft.AspNetCore.SignalR.Client;

namespace Api.Tests.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class ChatHubInProcessRealtimeIntegrationTests(PostgresTestcontainerFixture postgres)
    : ChatIntegrationTestBase(postgres)
{
    // --- SignalR hub (in-process, Redis off) ---

    [Fact]
    public async Task PostMessage_PushesMessageCreated_ToJoinedHubClient_WithoutRedis()
    {
        const string testMethodName = nameof(PostMessage_PushesMessageCreated_ToJoinedHubClient_WithoutRedis);

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
        await hubConnection.StartAsync(TestContext.Current.CancellationToken);
        await hubConnection.InvokeAsync("JoinRoom", room.Id, TestContext.Current.CancellationToken);

        // Act
        var createRes = await PostMessageAsync(room.Id, "In-process realtime");

        // Assert
        await IntegrationAssertions.AssertStatusAsync(createRes, HttpStatusCode.Created);
        var pushed = await received.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal("In-process realtime", pushed.Body);
        Assert.Equal(room.Id, pushed.ChatRoomId);
        Assert.Equal(userA.Id, pushed.SenderUserId);

        await hubConnection.DisposeAsync();
    }
}
