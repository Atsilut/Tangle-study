namespace Social.Friendships.Dto;

public record FriendshipGetResponseDto(
    long Id,
    long OtherUserId,
    string OtherUserNickname,
    DateTime CreatedAt,
    DateTime UpdatedAt);
