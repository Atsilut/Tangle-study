using Group.Entities;

namespace Group.Dto
{
    public record GroupBoardGetResponseDto(
        long Id,
        long GroupId,
        string Name,
        string? Description,
        BoardVisibility Visibility,
        BoardWriteability Writeability,
        bool CanWrite,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}
