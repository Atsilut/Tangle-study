using System.ComponentModel.DataAnnotations;
using Chat.Client;
using Chat.Entities;

namespace Chat.Dto;

public record ChatMessageCreateRequestDto
{
    [MaxLength(ChatMessage.MaxBodyLength)]
    public string Body { get; init; } = string.Empty;

    public long? MediaAssetId { get; init; }
}

public record ChatMessagePatchRequestDto
{
    [MaxLength(ChatMessage.MaxBodyLength)]
    public required string Body { get; init; }
}

public record ChatMessageMarkSeenRequestDto
{
    public required long[] MessageIds { get; init; }
}

public record ChatMessageEditGetResponseDto(
    long Id,
    string Body,
    DateTime RecordedAt,
    List<ChatMessageEditGetResponseDto> PreviousEdits);

public record ChatMessageGetResponseDto(
    long Id,
    long ChatRoomId,
    long SenderUserId,
    string SenderNickname,
    string Body,
    DateTime SentAt,
    DateTime UpdatedAt,
    bool IsDeleted,
    bool IsEdited,
    bool CanEdit,
    bool CanDelete,
    ChatMessageEditGetResponseDto? EditHistory,
    MediaAssetGetResponseDto? Media);
