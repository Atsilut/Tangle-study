using Api.Domain.Chat.Domain;
using Api.Domain.Chat.Service;
using Api.Global.Exceptions;
using Api.Tests.Infrastructure;
using Api.Tests.Repositories;

namespace Api.Tests.Services;

public sealed class ChatRoomAccessServiceUnitTests
{
    private static ChatRoomAccessService CreateService()
    {
        var http = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(http);
        return new ChatRoomAccessService(
            graph.FriendshipService,
            graph.UserBlockService,
            graph.UserService,
            graph.GroupMembershipService,
            graph.GroupService);
    }

    [Fact]
    public void EnsureParticipantInRoom_DoesNotThrow_WhenUserIsParticipant()
    {
        // Arrange
        var service = CreateService();
        var participants = new List<ChatRoomParticipant>
        {
            new(chatRoomId: 1, userId: 2, role: ChatRoomParticipantRole.Member),
        };

        // Act / Assert
        service.EnsureParticipantInRoom(participants, userId: 2);
    }

    [Fact]
    public void EnsureParticipantInRoom_ThrowsUnauthorized_WhenUserIsNotParticipant()
    {
        // Arrange
        var service = CreateService();
        var participants = new List<ChatRoomParticipant>
        {
            new(chatRoomId: 1, userId: 2, role: ChatRoomParticipantRole.Member),
        };

        // Act / Assert
        Assert.Throws<UnauthorizedAccessException>(() => service.EnsureParticipantInRoom(participants, userId: 999));
    }

    [Fact]
    public void EnsureRoomOwner_ThrowsUnauthorized_WhenParticipantIsNotOwner()
    {
        // Arrange
        var service = CreateService();
        var participants = new List<ChatRoomParticipant>
        {
            new(chatRoomId: 1, userId: 2, role: ChatRoomParticipantRole.Member),
        };

        // Act / Assert
        Assert.Throws<UnauthorizedAccessException>(() => service.EnsureRoomOwner(participants, userId: 2));
    }

    [Fact]
    public void EnsureRoomOwner_ThrowsEntityNotFound_WhenParticipantMissing()
    {
        // Arrange
        var service = CreateService();
        var participants = new List<ChatRoomParticipant>
        {
            new(chatRoomId: 1, userId: 2, role: ChatRoomParticipantRole.Owner),
        };

        // Act / Assert
        Assert.Throws<EntityNotFoundException>(() => service.EnsureRoomOwner(participants, userId: 999));
    }

    public enum AddParticipantOutcome
    {
        Ok,
        Unauthorized,
        NotFound,
    }

    public static TheoryData<ChatRoomKind, bool, ChatRoomParticipantRole, AddParticipantOutcome> EnsureCanAddParticipantMatrixData =>
        new()
        {
            { ChatRoomKind.Direct,         true,  ChatRoomParticipantRole.Member, AddParticipantOutcome.Ok },
            { ChatRoomKind.Direct,         false, ChatRoomParticipantRole.Member, AddParticipantOutcome.Unauthorized },
            { ChatRoomKind.Multi,          true,  ChatRoomParticipantRole.Member, AddParticipantOutcome.Ok },
            { ChatRoomKind.Multi,          false, ChatRoomParticipantRole.Member, AddParticipantOutcome.Unauthorized },
            { ChatRoomKind.PlatformGroup,  true,  ChatRoomParticipantRole.Owner,  AddParticipantOutcome.Ok },
            { ChatRoomKind.PlatformGroup,  true,  ChatRoomParticipantRole.Member, AddParticipantOutcome.Unauthorized },
            { ChatRoomKind.PlatformGroup,  false, ChatRoomParticipantRole.Owner,  AddParticipantOutcome.NotFound },
        };

    [Theory]
    [MemberData(nameof(EnsureCanAddParticipantMatrixData))]
    public void EnsureCanAddParticipant_Matrix(
        ChatRoomKind roomKind,
        bool actorInParticipants,
        ChatRoomParticipantRole actorRole,
        AddParticipantOutcome expectedOutcome)
    {
        // Arrange
        var service = CreateService();
        var room = roomKind switch
        {
            ChatRoomKind.Direct => ChatRoom.CreateDirect(userAId: 1, userBId: 2, createdByUserId: 1),
            ChatRoomKind.Multi => ChatRoom.CreateMulti(title: null, createdByUserId: 1),
            ChatRoomKind.PlatformGroup => ChatRoom.CreatePlatformGroup(title: null, platformGroupId: 9, createdByUserId: 1),
            _ => throw new ArgumentOutOfRangeException(nameof(roomKind), roomKind, null),
        };

        var participants = new List<ChatRoomParticipant>
        {
            // Another participant always exists so we can distinguish "missing actor" from "wrong role".
            new(chatRoomId: 1, userId: 2, role: ChatRoomParticipantRole.Member),
        };

        const long actorId = 123;
        if (actorInParticipants)
            participants.Add(new(chatRoomId: 1, userId: actorId, role: actorRole));

        // Act
        var action = () => service.EnsureCanAddParticipant(room, participants, actorUserId: actorId);

        // Assert
        switch (expectedOutcome)
        {
            case AddParticipantOutcome.Ok:
                action();
                break;
            case AddParticipantOutcome.Unauthorized:
                Assert.Throws<UnauthorizedAccessException>(action);
                break;
            case AddParticipantOutcome.NotFound:
                Assert.Throws<EntityNotFoundException>(action);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(expectedOutcome), expectedOutcome, null);
        }
    }
}

