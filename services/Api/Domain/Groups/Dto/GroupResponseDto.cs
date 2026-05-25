using Api.Domain.Groups.Domain;

namespace Api.Domain.Groups.Dto
{
    public record GroupResponseDto(
        long Id,
        string Name,
        string Description,
        GroupVisibility Visibility,
        GroupJoinPolicy JoinPolicy,
        int MemberCount,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    public record GroupMemberResponseDto(
        long UserId,
        string Nickname,
        GroupRole Role,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}
