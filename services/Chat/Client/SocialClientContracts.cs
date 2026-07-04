namespace Chat.Client;

internal sealed record SocialUserPairRequestDto(long UserId, long OtherUserId);

internal sealed record SocialMutualBlocksRequestDto(long UserId, long[] OtherUserIds);
