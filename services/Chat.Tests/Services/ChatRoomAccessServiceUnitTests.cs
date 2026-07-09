using Chat.Entities;
using Chat.Service;
using Chat.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Tangle.AspNetCore.Exceptions;

namespace Chat.Tests.Services;

public sealed class ChatRoomAccessServiceUnitTests
{
    private static (ChatRoomAccessService Service, InMemoryUserClient Monolith, FakeHttpContextAccessor Http) CreateGraph()
    {
        var http = new FakeHttpContextAccessor("1");
        var monolith = new InMemoryUserClient(http);
        var service = new ChatRoomAccessService(monolith, monolith, monolith);
        return (service, monolith, http);
    }

    private static void SetCaller(FakeHttpContextAccessor http, long userId)
    {
        http.HttpContext = new DefaultHttpContext
        {
            User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(
                    [new System.Security.Claims.Claim("sub", userId.ToString())],
                    authenticationType: "Test")),
        };
    }

    [Fact]
    public async Task EnsureCanCreateMultiRoomAsync_ThrowsWhenBlockExistsBetweenCreatorAndParticipant()
    {
        // Arrange
        var (service, monolith, http) = CreateGraph();
        var creator = monolith.CreateUser("creator");
        var blocked = monolith.CreateUser("blocked");
        SetCaller(http, creator.Id);
        monolith.AddBlock(creator.Id, blocked.Id);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.EnsureCanCreateMultiRoomAsync(creator.Id, [creator.Id, blocked.Id]));
        Assert.Contains("block exists", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EnsureInviteeCanBeAddedAsync_ThrowsWhenBlockExistsBetweenInviteeAndParticipant()
    {
        // Arrange — adder has no block with participants; invitee is blocked with an existing member.
        var (service, monolith, http) = CreateGraph();
        var adder = monolith.CreateUser("adder");
        var participant = monolith.CreateUser("participant");
        var invitee = monolith.CreateUser("invitee");
        SetCaller(http, adder.Id);
        monolith.AddBlock(invitee.Id, participant.Id);

        var room = ChatRoom.CreateMulti(title: null, createdByUserId: adder.Id);
        var participants = new List<ChatRoomParticipant>
        {
            new(chatRoomId: 1, userId: adder.Id, role: ChatRoomParticipantRole.Member),
            new(chatRoomId: 1, userId: participant.Id, role: ChatRoomParticipantRole.Member),
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.EnsureInviteeCanBeAddedAsync(room, invitee.Id, participants));
        Assert.Contains("block exists", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EnsureCanCreatePlatformGroupRoomAsync_ThrowsWhenParticipantIsNotGroupMember()
    {
        // Arrange
        var (service, monolith, http) = CreateGraph();
        var creator = monolith.CreateUser("creator");
        var outsider = monolith.CreateUser("outsider");
        var groupId = monolith.CreateGroup();
        monolith.AddGroupMember(groupId, creator.Id);
        SetCaller(http, creator.Id);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.EnsureCanCreatePlatformGroupRoomAsync(groupId, creator.Id, [creator.Id, outsider.Id]));
        Assert.Contains("members of this group", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnsureParticipantInRoom_DoesNotThrow_WhenUserIsParticipant()
    {
        // Arrange
        var (service, _, _) = CreateGraph();
        var participants = new List<ChatRoomParticipant>
        {
            new(chatRoomId: 1, userId: 2, role: ChatRoomParticipantRole.Member),
        };

        // Act & Assert
        service.EnsureParticipantInRoom(participants, userId: 2);
    }

    [Fact]
    public void EnsureParticipantInRoom_ThrowsUnauthorized_WhenUserIsNotParticipant()
    {
        // Arrange
        var (service, _, _) = CreateGraph();
        var participants = new List<ChatRoomParticipant>
        {
            new(chatRoomId: 1, userId: 2, role: ChatRoomParticipantRole.Member),
        };

        // Act & Assert
        Assert.Throws<UnauthorizedAccessException>(() => service.EnsureParticipantInRoom(participants, userId: 999));
    }

    [Fact]
    public void EnsureRoomOwner_ThrowsUnauthorized_WhenParticipantIsNotOwner()
    {
        // Arrange
        var (service, _, _) = CreateGraph();
        var participants = new List<ChatRoomParticipant>
        {
            new(chatRoomId: 1, userId: 2, role: ChatRoomParticipantRole.Member),
        };

        // Act & Assert
        Assert.Throws<UnauthorizedAccessException>(() => service.EnsureRoomOwner(participants, userId: 2));
    }

    [Fact]
    public void EnsureRoomOwner_ThrowsEntityNotFound_WhenParticipantMissing()
    {
        // Arrange
        var (service, _, _) = CreateGraph();
        var participants = new List<ChatRoomParticipant>
        {
            new(chatRoomId: 1, userId: 2, role: ChatRoomParticipantRole.Owner),
        };

        // Act & Assert
        Assert.Throws<EntityNotFoundException>(() => service.EnsureRoomOwner(participants, userId: 999));
    }
}
