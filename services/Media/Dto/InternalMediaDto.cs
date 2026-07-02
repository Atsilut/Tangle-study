using System.ComponentModel.DataAnnotations;

namespace Media.Dto;

public sealed record LinkPostMediaRequestDto(
    long PostId,
    long UploaderUserId,
    IReadOnlyList<long>? MediaAssetIds);

public sealed record PatchPostMediaRequestDto(
    long PostId,
    long UploaderUserId,
    IReadOnlyList<long>? AddMediaAssetIds,
    IReadOnlyList<long>? RemoveMediaAssetIds);

public sealed record LinkCommentMediaRequestDto(
    long CommentId,
    long UploaderUserId,
    long? MediaAssetId);

public sealed record LinkChatMessageMediaRequestDto(
    long ChatMessageId,
    long SenderUserId,
    long? MediaAssetId);

public sealed record BatchPostIdsRequestDto(
    [Required] IReadOnlyList<long> PostIds);

public sealed record BatchCommentIdsRequestDto(
    [Required] IReadOnlyList<long> CommentIds);

public sealed record BatchChatMessageIdsRequestDto(
    [Required] IReadOnlyList<long> ChatMessageIds);

public sealed record DeletePostsMediaRequestDto(
    [Required] IReadOnlyList<long> PostIds);

public sealed record DeleteCommentsMediaRequestDto(
    [Required] IReadOnlyList<long> CommentIds);
