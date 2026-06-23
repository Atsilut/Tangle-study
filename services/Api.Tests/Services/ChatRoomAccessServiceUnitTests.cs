using Api.Domain.Chat.Domain;
using Api.Domain.Chat.Service;
using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Domain.UserBlocks.Dto;
using Api.Global.Exceptions;
using Api.Tests.Infrastructure;
using Api.Tests.Repositories;

namespace Api.Tests.Services;

public sealed class ChatRoomAccessServiceUnitTests
{
    private static (ChatRoomAccessService Service, DomainServiceTestFactory.Graph Graph, FakeHttpContextAccessor Http) CreateGraph()
    {
        var http = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(http);
        var service = new ChatRoomAccessService(
            graph.FriendshipService,
            graph.UserBlockService,
            graph.UserService,
            graph.GroupMembershipService,
            graph.GroupService);
        return (service, graph, http);
    }

    [Fact]
    public async Task EnsureCanCreateMultiRoomAsync_ThrowsWhenBlockExistsBetweenCreatorAndParticipant()
    {
        // Arrange
        var (service, graph, http) = CreateGraph();
        var creator = await ServiceTestHelpers.CreateUserAsync(graph.UserRepository, "creator");
        var blocked = await ServiceTestHelpers.CreateUserAsync(graph.UserRepository, "blocked");
        http.HttpContext = ServiceTestHelpers.ContextFor(creator.Id);
        await graph.UserBlockService.BlockUserAsync(new UserBlockCreateRequestDto { BlockedUserId = blocked.Id });

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.EnsureCanCreateMultiRoomAsync(creator.Id, [creator.Id, blocked.Id]));
        Assert.Contains("block exists", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EnsureCanCreatePlatformGroupRoomAsync_ThrowsWhenParticipantIsNotGroupMember()
    {
        // Arrange
        var (service, graph, http) = CreateGraph();
        var creator = await ServiceTestHelpers.CreateUserAsync(graph.UserRepository, "creator");
        var outsider = await ServiceTestHelpers.CreateUserAsync(graph.UserRepository, "outsider");
        http.HttpContext = ServiceTestHelpers.ContextFor(creator.Id);
        var group = await graph.GroupService.CreateGroupAsync(new GroupCreateRequestDto
        {
            Name = "G",
            Description = "d",
            Visibility = GroupVisibility.Private,
        });

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.EnsureCanCreatePlatformGroupRoomAsync(group.Id, creator.Id, [creator.Id, outsider.Id]));
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
