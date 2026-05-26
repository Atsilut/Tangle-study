using Api.Domain.Groups.Domain;

namespace Api.Domain.Groups.Dto
{
    public record GroupBoardResponseDto(
        long Id,
        long GroupId,
        string Name,
        string? Description,
        BoardVisibility Visibility,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}
