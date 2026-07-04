namespace Api.Global.Dto;

public sealed record InternalAccessUserIdsRequestDto(long[] UserIds);

public sealed record InternalAccessOtherUserRequestDto(long OtherUserId);

public sealed record InternalAccessGroupMembersRequestDto(long[] UserIds);

public sealed record InternalAccessNicknameEntryDto(long UserId, string Nickname);

public sealed record InternalAccessNicknamesResponseDto(IReadOnlyList<InternalAccessNicknameEntryDto> Nicknames);

public sealed record InternalAccessViewablePostsRequestDto(long[] PostIds, long? ViewerUserId);

public sealed record InternalAccessViewablePostsResponseDto(long[] ViewablePostIds);

public sealed record InternalAccessMutualBlocksRequestDto(long UserId, long[] OtherUserIds);

public sealed record InternalAccessMutualBlocksResponseDto(long[] BlockedUserIds);

public sealed record InternalAccessGroupMemberEntryDto(long UserId, string Nickname);

public sealed record InternalAccessGroupMembersResponseDto(IReadOnlyList<InternalAccessGroupMemberEntryDto> Members);

public sealed record InternalAccessGroupMemberIdsResponseDto(long[] MemberUserIds);
