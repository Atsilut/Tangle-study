namespace Chat.Client;

internal sealed record InternalAccessUserIdsRequestDto(long[] UserIds);

internal sealed record InternalAccessOtherUserRequestDto(long OtherUserId);

internal sealed record InternalAccessGroupMembersRequestDto(long[] UserIds);

internal sealed record InternalAccessNicknameEntryDto(long UserId, string Nickname);

internal sealed record InternalAccessNicknamesResponseDto(IReadOnlyList<InternalAccessNicknameEntryDto> Nicknames);
