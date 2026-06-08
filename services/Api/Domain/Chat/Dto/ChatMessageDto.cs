using System.ComponentModel.DataAnnotations;
using Api.Domain.Chat.Domain;
using Api.Domain.Media.Dto;

namespace Api.Domain.Chat.Dto;

public record ChatMessageCreateRequestDto
{
    [Required]
    [MaxLength(ChatMessage.MaxBodyLength)]
    public required string Body { get; init; } = string.Empty;

    public long? MediaAssetId { get; init; }
}

public record ChatMessageGetResponseDto(
    long Id,
    long ChatRoomId,
    long SenderUserId,
    string SenderNickname,
    string Body,
    DateTime SentAt,
    MediaAssetGetResponseDto? Media);
