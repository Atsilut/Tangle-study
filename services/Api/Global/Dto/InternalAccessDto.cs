namespace Api.Global.Dto;

public sealed record InternalAccessUserIdsRequestDto(long[] UserIds);

public sealed record InternalAccessOtherUserRequestDto(long OtherUserId);

public sealed record InternalAccessNicknameEntryDto(long UserId, string Nickname);

public sealed record InternalAccessNicknamesResponseDto(IReadOnlyList<InternalAccessNicknameEntryDto> Nicknames);

public sealed record InternalAccessNicknameLookupRequestDto(string Nickname);

public sealed record InternalAccessNicknameLookupResponseDto(long UserId);

public sealed record InternalAccessMutualBlocksRequestDto(long UserId, long[] OtherUserIds);

public sealed record InternalAccessMutualBlocksResponseDto(long[] BlockedUserIds);

public sealed record InternalAccessIsBlockedRequestDto(long BlockerUserId, long BlockedUserId);

public sealed record InternalAccessIsBlockedResponseDto(bool IsBlocked);
