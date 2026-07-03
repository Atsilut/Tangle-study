namespace Location.Client;

public record PostLocationGetResponseDto(decimal Latitude, decimal Longitude);

public record GroupMemberSummaryDto(long UserId, string Nickname);

internal sealed record InternalAccessUserIdsRequestDto(long[] UserIds);

internal sealed record InternalAccessOtherUserRequestDto(long OtherUserId);

internal sealed record InternalAccessGroupMembersRequestDto(long[] UserIds);

internal sealed record InternalAccessNicknameEntryDto(long UserId, string Nickname);

internal sealed record InternalAccessNicknamesResponseDto(IReadOnlyList<InternalAccessNicknameEntryDto> Nicknames);

internal sealed record InternalAccessViewablePostsRequestDto(long[] PostIds, long? ViewerUserId);

internal sealed record InternalAccessViewablePostsResponseDto(long[] ViewablePostIds);

internal sealed record InternalAccessMutualBlocksRequestDto(long UserId, long[] OtherUserIds);

internal sealed record InternalAccessMutualBlocksResponseDto(long[] BlockedUserIds);

internal sealed record InternalAccessGroupMemberEntryDto(long UserId, string Nickname);

internal sealed record InternalAccessGroupMembersResponseDto(IReadOnlyList<InternalAccessGroupMemberEntryDto> Members);

internal sealed record InternalAccessGroupMemberIdsResponseDto(long[] MemberUserIds);
