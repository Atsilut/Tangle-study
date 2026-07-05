using Users.Domain;

namespace Users.Dto;

public sealed record InternalUsersUserIdsRequestDto(long[] UserIds);

public sealed record InternalUsersNicknameEntryDto(long UserId, string Nickname);

public sealed record InternalUsersNicknamesResponseDto(IReadOnlyList<InternalUsersNicknameEntryDto> Nicknames);

public sealed record InternalUsersNicknameLookupRequestDto(string Nickname);

public sealed record InternalUsersNicknameLookupResponseDto(long UserId);

public sealed record InternalUsersFriendsListVisibilityResponseDto(
    FriendsListVisibility FriendsListVisibility);
