namespace Location.Client;

public record PostLocationGetResponseDto(decimal Latitude, decimal Longitude);

public record GroupMemberSummaryDto(long UserId, string Nickname);

internal sealed record InternalAccessUserIdsRequestDto(long[] UserIds);

internal sealed record InternalAccessOtherUserRequestDto(long OtherUserId);

internal sealed record InternalAccessNicknameEntryDto(long UserId, string Nickname);

internal sealed record InternalAccessNicknamesResponseDto(IReadOnlyList<InternalAccessNicknameEntryDto> Nicknames);
