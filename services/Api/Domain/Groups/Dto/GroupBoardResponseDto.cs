using Api.Domain.Groups.Domain;

namespace Api.Domain.Groups.Dto
{
    public record GroupBoardResponseDto(
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
