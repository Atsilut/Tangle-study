using Api.Domain.Users.Domain;

namespace Api.Global.Dto;

public sealed record InternalAccessUserIdsRequestDto(long[] UserIds);

public sealed record InternalAccessNicknameEntryDto(long UserId, string Nickname);

public sealed record InternalAccessNicknamesResponseDto(IReadOnlyList<InternalAccessNicknameEntryDto> Nicknames);

public sealed record InternalAccessNicknameLookupRequestDto(string Nickname);

public sealed record InternalAccessNicknameLookupResponseDto(long UserId);

public sealed record InternalAccessFriendsListVisibilityResponseDto(
    FriendsListVisibility FriendsListVisibility);
