using Chat.Entities;

namespace Chat.Dto;

public record ChatRoomParticipantGetResponseDto(
    long Id,
    long UserId,
    string Nickname,
    ChatRoomParticipantRole Role,
    DateTime JoinedAt);

public record ChatRoomGetResponseDto(
    long Id,
    ChatRoomKind Kind,
    string? Title,
    long? PlatformGroupId,
    long CreatedByUserId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<ChatRoomParticipantGetResponseDto> Participants);

public record ChatRoomSummaryLastMessageDto(
    long SenderUserId,
    string Body,
    string SenderNickname,
    DateTime SentAt,
    bool HasMedia);

public record ChatRoomSummaryGetResponseDto(
    long Id,
    ChatRoomKind Kind,
    string? Title,
    long? PlatformGroupId,
    DateTime UpdatedAt,
    IReadOnlyList<string> OtherParticipantNicknames,
    ChatRoomSummaryLastMessageDto? LastMessage);
