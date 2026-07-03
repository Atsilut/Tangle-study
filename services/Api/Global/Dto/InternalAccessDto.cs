namespace Api.Global.Dto;

public sealed record InternalAccessUserIdsRequestDto(long[] UserIds);

public sealed record InternalAccessOtherUserRequestDto(long OtherUserId);

public sealed record InternalAccessGroupMembersRequestDto(long[] UserIds);

public sealed record InternalAccessNicknameEntryDto(long UserId, string Nickname);

public sealed record InternalAccessNicknamesResponseDto(IReadOnlyList<InternalAccessNicknameEntryDto> Nicknames);
