namespace Social.Client;

internal sealed record InternalAccessUserIdsRequestDto(long[] UserIds);

internal sealed record InternalAccessNicknameEntryDto(long UserId, string Nickname);

internal sealed record InternalAccessNicknamesResponseDto(IReadOnlyList<InternalAccessNicknameEntryDto> Nicknames);

internal sealed record InternalAccessFriendsListVisibilityResponseDto(FriendsListVisibility FriendsListVisibility);
