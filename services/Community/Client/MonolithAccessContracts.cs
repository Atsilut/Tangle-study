namespace Community.Client;

internal sealed record InternalAccessUserIdsRequestDto(long[] UserIds);

internal sealed record InternalAccessNicknameEntryDto(long UserId, string Nickname);

internal sealed record InternalAccessNicknamesResponseDto(IReadOnlyList<InternalAccessNicknameEntryDto> Nicknames);

internal sealed record InternalAccessNicknameLookupRequestDto(string Nickname);

internal sealed record InternalAccessNicknameLookupResponseDto(long UserId);

internal sealed record InternalAccessMutualBlocksRequestDto(long UserId, long[] OtherUserIds);

internal sealed record InternalAccessMutualBlocksResponseDto(long[] BlockedUserIds);
