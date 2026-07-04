namespace Community.Client;

internal sealed record SocialMutualBlocksRequestDto(long UserId, long[] OtherUserIds);

internal sealed record SocialMutualBlocksResponseDto(long[] BlockedUserIds);
