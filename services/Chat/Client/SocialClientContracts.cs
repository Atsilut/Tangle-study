namespace Chat.Client;

internal sealed record SocialOtherUserRequestDto(long OtherUserId);

internal sealed record SocialUserIdsRequestDto(long[] UserIds);
