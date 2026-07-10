using Group.Entities;

namespace Group.Dto
{
    public record GroupGetResponseDto(
        long Id,
        string Name,
        string Description,
        GroupVisibility Visibility,
        GroupJoinPolicy JoinPolicy,
        GroupInvitePolicy InvitePolicy,
        int MemberCount,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        bool IsLimitedProfile = false);

    public record GroupMemberGetResponseDto(
        long UserId,
        string Nickname,
        GroupRole Role,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}
