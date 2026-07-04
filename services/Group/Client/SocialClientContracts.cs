namespace Group.Client;

internal sealed record SocialIsBlockedRequestDto(long BlockerUserId, long BlockedUserId);

internal sealed record SocialIsBlockedResponseDto(bool IsBlocked);
