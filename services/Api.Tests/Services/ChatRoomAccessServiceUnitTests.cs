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

        // Act & Assert
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

        // Act & Assert
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

        // Act & Assert
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

        // Act & Assert
        Assert.Throws<EntityNotFoundException>(() => service.EnsureRoomOwner(participants, userId: 999));
    }
}
