using System.Net;
using System.Net.Http.Json;
using Chat.Dto;
using Chat.Tests.Infrastructure;

namespace Chat.Tests.Controllers;

[Collection(ChatIntegrationTestCollection.Name)]
public sealed class ChatNicknameIntegrationTests(
    PostgresTestcontainerFixture postgres,
    RedisTestcontainerFixture redis)
    : ChatIntegrationTestBase(postgres, redis)
{
    [Fact]
    public async Task ListMessages_ReturnsUpdatedNickname_AfterMonolithNicknameChanges()
    {
        const string testMethodName = nameof(ListMessages_ReturnsUpdatedNickname_AfterMonolithNicknameChanges);
        const string updatedNickname = "UpdatedChatNick";

        // Arrange
        var userA = CreateUserForTest(testMethodName, 1);
        var userB = CreateUserForTest(testMethodName, 2);
        AcceptFriendship(userA, userB);
        LoginAs(userA);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);
        var createRes = await PostMessageAsync(room.Id, "Nickname refresh");
        await IntegrationAssertions.AssertStatusAsync(createRes, HttpStatusCode.Created);

        InMemoryUser.Nicknames[userA.Id] = updatedNickname;

        // Act
        LoginAs(userB);
        var listRes = await Client.GetAsync($"{ChatRoomsBase}/{room.Id}/messages", TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(listRes, HttpStatusCode.OK);
        var messages = await listRes.Content.ReadFromJsonAsync<List<ChatMessageGetResponseDto>>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(messages);
        Assert.Contains(messages, m => m.SenderUserId == userA.Id && m.SenderNickname == updatedNickname);
    }
}
