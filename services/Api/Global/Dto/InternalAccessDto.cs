namespace Api.Global.Dto;

public sealed record InternalAccessUserIdsRequestDto(long[] UserIds);

public sealed record InternalAccessOtherUserRequestDto(long OtherUserId);

public sealed record InternalAccessGroupMembersRequestDto(long[] UserIds);

public sealed record InternalAccessNicknameEntryDto(long UserId, string Nickname);

public sealed record InternalAccessNicknamesResponseDto(IReadOnlyList<InternalAccessNicknameEntryDto> Nicknames);

public sealed record InternalAccessNicknameLookupRequestDto(string Nickname);

public sealed record InternalAccessNicknameLookupResponseDto(long UserId);

public sealed record InternalAccessBoardKeyDto(long GroupId, long BoardId);

public sealed record InternalAccessViewableBoardsRequestDto(InternalAccessBoardKeyDto[] Boards);

public sealed record InternalAccessViewableBoardsResponseDto(InternalAccessBoardKeyDto[] Viewable);

public sealed record InternalAccessMutualBlocksRequestDto(long UserId, long[] OtherUserIds);

public sealed record InternalAccessMutualBlocksResponseDto(long[] BlockedUserIds);

public sealed record InternalAccessGroupMemberEntryDto(long UserId, string Nickname);

public sealed record InternalAccessGroupMembersResponseDto(IReadOnlyList<InternalAccessGroupMemberEntryDto> Members);

public sealed record InternalAccessGroupMemberIdsResponseDto(long[] MemberUserIds);

public sealed record InternalAccessIsBlockedRequestDto(long BlockerUserId, long BlockedUserId);

public sealed record InternalAccessIsBlockedResponseDto(bool IsBlocked);
