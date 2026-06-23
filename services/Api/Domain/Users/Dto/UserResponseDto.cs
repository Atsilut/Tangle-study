using Api.Domain.Users.Domain;

namespace Api.Domain.Users.Dto
{
    public record UserGetResponseDto(
        long Id,
        string? Email,
        string Nickname,
        FriendsListVisibility FriendsListVisibility,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    public record UserPatchResponseDto(string Nickname, DateTime UpdatedAt);

    public record UserPrivacySettingsResponseDto(
        FriendsListVisibility FriendsListVisibility,
        DateTime UpdatedAt);

    public record NicknameAvailabilityResponseDto(bool Available);
}
