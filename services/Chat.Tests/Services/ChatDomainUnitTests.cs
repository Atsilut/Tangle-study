using Chat.Entities;

namespace Chat.Tests.Services;

public sealed class ChatDomainUnitTests
{
    [Fact]
    public void ChatRoom_CreateDirect_WithSameUserId_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => ChatRoom.CreateDirect(1, 1, createdByUserId: 1));
    }

    [Fact]
    public void ChatRoom_CreateDirect_SetsCanonicalPairAndKind()
    {
        // Arrange + Act
        var room = ChatRoom.CreateDirect(userAId: 10, userBId: 2, createdByUserId: 5);

        // Assert
        Assert.Equal(ChatRoomKind.Direct, room.Kind);
        Assert.Equal(2, room.UserLowId);
        Assert.Equal(10, room.UserHighId);
        Assert.Null(room.PlatformGroupId);
    }

    [Fact]
    public void ChatRoom_PromoteDirectToMulti_WhenDirect_SetsKindAndClearsPairColumns()
    {
        // Arrange
        var room = ChatRoom.CreateDirect(userAId: 10, userBId: 2, createdByUserId: 5);

        // Act
        room.PromoteDirectToMulti();

        // Assert
        Assert.Equal(ChatRoomKind.Multi, room.Kind);
        Assert.Null(room.UserLowId);
        Assert.Null(room.UserHighId);
    }

    [Fact]
    public void ChatRoom_PromoteDirectToMulti_WhenNotDirect_ThrowsInvalidOperationException()
    {
        // Arrange
        var room = ChatRoom.CreateMulti(title: "t", createdByUserId: 5);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => room.PromoteDirectToMulti());
    }

    [Fact]
    public void ChatRoom_OtherDirectPartyId_WhenUserIsInRoom_ReturnsOtherParty()
    {
        // Arrange
        var room = ChatRoom.CreateDirect(userAId: 10, userBId: 2, createdByUserId: 5);

        // Act
        var other = room.OtherDirectPartyId(userId: 10);

        // Assert
        Assert.Equal(2, other);
    }

    [Fact]
    public void ChatRoom_OtherDirectPartyId_WhenRoomIsNotDirect_ThrowsInvalidOperationException()
    {
        // Arrange
        var room = ChatRoom.CreateMulti(title: null, createdByUserId: 5);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => room.OtherDirectPartyId(userId: 1));
    }

    [Fact]
    public void ChatRoom_OtherDirectPartyId_WhenUserNotInRoom_ThrowsArgumentException()
    {
        // Arrange
        var room = ChatRoom.CreateDirect(userAId: 10, userBId: 2, createdByUserId: 5);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => room.OtherDirectPartyId(userId: 999));
    }

    [Fact]
    public void ChatRoom_HasRoomOwner_IsTrue_OnlyForPlatformGroup()
    {
        // Arrange + Act
        var direct = ChatRoom.CreateDirect(userAId: 1, userBId: 2, createdByUserId: 1);
        var multi = ChatRoom.CreateMulti(title: null, createdByUserId: 1);
        var platform = ChatRoom.CreatePlatformGroup(title: null, platformGroupId: 99, createdByUserId: 1);

        // Assert
        Assert.False(direct.HasRoomOwner);
        Assert.False(multi.HasRoomOwner);
        Assert.True(platform.HasRoomOwner);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void ChatMessage_Constructor_AllowsEmptyBody(string body)
    {
        var msg = new ChatMessage(chatRoomId: 1, senderUserId: 2, body);
        Assert.Equal(body, msg.Body);
    }

    [Fact]
    public void ChatMessage_Constructor_WithOverMaxBodyLength_ThrowsArgumentException()
    {
        // Arrange
        var tooLong = new string('a', ChatMessage.MaxBodyLength + 1);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new ChatMessage(chatRoomId: 1, senderUserId: 2, tooLong));
    }

    [Fact]
    public void ChatMessage_Constructor_WithValidBody_SetsProperties()
    {
        // Arrange
        const string body = "Hello";

        // Act
        var msg = new ChatMessage(chatRoomId: 1, senderUserId: 2, body);

        // Assert
        Assert.Equal(1, msg.ChatRoomId);
        Assert.Equal(2, msg.SenderUserId);
        Assert.Equal(body, msg.Body);
    }

    [Fact]
    public void ChatRoomParticipant_IsOwner_IsTrue_WhenRoleIsOwner()
    {
        // Arrange
        var participant = new ChatRoomParticipant(chatRoomId: 1, userId: 2, role: ChatRoomParticipantRole.Owner);

        // Assert
        Assert.True(participant.IsOwner);
    }

    [Fact]
    public void ChatRoomParticipant_IsOwner_IsFalse_WhenRoleIsMember()
    {
        // Arrange
        var participant = new ChatRoomParticipant(chatRoomId: 1, userId: 2, role: ChatRoomParticipantRole.Member);

        // Assert
        Assert.False(participant.IsOwner);
    }

    [Fact]
    public void ChatRoomParticipant_ChangeRole_UpdatesRole()
    {
        // Arrange
        var participant = new ChatRoomParticipant(chatRoomId: 1, userId: 2, role: ChatRoomParticipantRole.Member);

        // Act
        participant.ChangeRole(ChatRoomParticipantRole.Owner);

        // Assert
        Assert.True(participant.IsOwner);
        Assert.Equal(ChatRoomParticipantRole.Owner, participant.Role);
    }
}

