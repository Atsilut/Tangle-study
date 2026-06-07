using System.ComponentModel.DataAnnotations;
using Api.Domain.Chat.Domain;

namespace Api.Domain.Chat.Dto;

public record ChatMessageCreateRequestDto
{
    [Required]
    [MaxLength(ChatMessage.MaxBodyLength)]
    public required string Body { get; init; } = string.Empty;
}

public record ChatMessageGetResponseDto(
    long Id,
    long ChatRoomId,
    long SenderUserId,
    string SenderNickname,
    string Body,
    DateTime SentAt);
