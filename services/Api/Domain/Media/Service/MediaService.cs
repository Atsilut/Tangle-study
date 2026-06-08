using Api.Domain.Media.Domain;
using Api.Domain.Media.Dto;
using Api.Domain.Media.Repository;
using Api.Domain.Media.Storage;
using Api.Domain.Users.Service;
using Api.Global.Config;
using Api.Global.Exceptions;
using Api.Global.Infrastructure;
using Api.Global.Queue;
using Microsoft.Extensions.Options;

namespace Api.Domain.Media.Service;

[Service]
public sealed class MediaService(
    IMediaAssetRepository repo,
    IMediaStorage mediaStorage,
    MediaLimitPolicy limitPolicy,
    UserService userService,
    IWorkQueue workQueue,
    IOptions<MediaOptions> mediaOptions,
    IHttpContextAccessor httpContextAccessor)
{
    private static readonly TimeSpan PresignedUploadExpiry = TimeSpan.FromHours(1);

    private readonly IMediaAssetRepository _repo = repo;
    private readonly IMediaStorage _mediaStorage = mediaStorage;
    private readonly MediaLimitPolicy _limitPolicy = limitPolicy;
    private readonly UserService _userService = userService;
    private readonly IWorkQueue _workQueue = workQueue;
    private readonly MediaOptions _mediaOptions = mediaOptions.Value;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    private long GetUserIdFromLogin() => long.Parse(_httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
        ?? throw new UnauthorizedAccessException("Unauthorized access"));

    public async Task<MediaAssetGetResponseDto> GetMediaAssetByIdAsync(long id)
    {
        var asset = await GetOwnedOrAccessibleAssetAsync(id);
        return MapToDto(asset);
    }

    public async Task DeleteUnlinkedMediaAssetByIdAsync(long id)
    {
        EnsureMediaEnabled();
        var userId = GetUserIdFromLogin();
        var asset = await GetMediaAssetOrThrowAsync(id);
        if (asset.UploaderId != userId) throw new UnauthorizedAccessException("Unauthorized access");
        if (asset.IsLinked) throw new ArgumentException("Cannot delete media that is linked to content.");
        if (asset.ProcessingStatus is MediaProcessingStatus.Processing)
            throw new ArgumentException("Cannot delete media while processing.");

        await DeleteBlobStorageForAssetsAsync([asset]);
        await _repo.DeleteMediaAssetAsync(asset);
    }

    internal async Task DeleteBlobStorageForPostAsync(long postId)
    {
        var assets = await _repo.GetMediaAssetsByPostIdAsync(postId);
        await DeleteBlobStorageForAssetsAsync(assets);
    }

    internal async Task DeleteBlobStorageForPostsAsync(IReadOnlyCollection<long> postIds)
    {
        if (postIds.Count == 0) return;

        List<MediaAsset> assets = [];
        foreach (var postId in postIds)
            assets.AddRange(await _repo.GetMediaAssetsByPostIdAsync(postId));

        await DeleteBlobStorageForAssetsAsync(assets);
    }

    internal async Task DeleteBlobStorageForCommentAsync(long commentId)
    {
        var assets = await _repo.GetMediaAssetsByCommentIdAsync(commentId);
        await DeleteBlobStorageForAssetsAsync(assets);
    }

    internal async Task DeleteBlobStorageForCommentsAsync(IReadOnlyCollection<long> commentIds)
    {
        var assets = await _repo.GetMediaAssetsByCommentIdsAsync(commentIds);
        await DeleteBlobStorageForAssetsAsync(assets);
    }

    internal async Task DeleteBlobStorageForChatMessageAsync(long chatMessageId)
    {
        var assets = await _repo.GetMediaAssetsByChatMessageIdAsync(chatMessageId);
        await DeleteBlobStorageForAssetsAsync(assets);
    }

    internal Task DetachUploaderFromDeletedUserAsync(long uploaderId) =>
        _repo.DetachUploaderFromMediaAssetsAsync(uploaderId);

    private async Task DeleteBlobStorageForAssetsAsync(IReadOnlyList<MediaAsset> assets)
    {
        foreach (var asset in assets)
        {
            if (await _mediaStorage.ObjectExistsAsync(asset.OriginalObjectKey))
                await _mediaStorage.DeleteObjectAsync(asset.OriginalObjectKey);

            if (!string.IsNullOrWhiteSpace(asset.ProcessedObjectKey)
                && await _mediaStorage.ObjectExistsAsync(asset.ProcessedObjectKey))
                await _mediaStorage.DeleteObjectAsync(asset.ProcessedObjectKey);
        }
    }

    public async Task<MediaUploadInitResponseDto> InitUploadAsync(MediaUploadInitRequestDto request)
    {
        EnsureMediaEnabled();
        var userId = GetUserIdFromLogin();
        await _userService.EnsureUserExistsAsync(userId, "Authentication failed", StatusCodes.Status400BadRequest);

        var kind = _limitPolicy.ClassifyKind(request.MimeType);
        _limitPolicy.EnsureWithinIngressLimit(request.IntendedContext, kind, request.SizeBytes);

        var storageLimits = _limitPolicy.GetStorageLimits(request.IntendedContext, kind);
        var objectKey = BuildObjectKey(userId, request.FileName);
        var asset = MediaAsset.CreatePendingUpload(
            userId,
            request.IntendedContext,
            kind,
            request.MimeType,
            request.FileName,
            objectKey,
            request.SizeBytes);

        await _repo.CreateMediaAssetAsync(asset);

        var presigned = await _mediaStorage.CreatePresignedUploadAsync(
            objectKey,
            request.MimeType,
            PresignedUploadExpiry);

        return new MediaUploadInitResponseDto(
            asset.Id,
            presigned.Url,
            presigned.ObjectKey,
            presigned.ExpiresAt,
            _limitPolicy.GetIngressLimit(request.IntendedContext, kind),
            storageLimits.PerFileBytes);
    }

    public async Task<MediaAssetGetResponseDto> CompleteUploadAsync(long id)
    {
        EnsureMediaEnabled();
        var userId = GetUserIdFromLogin();
        var asset = await GetMediaAssetOrThrowAsync(id);
        if (asset.UploaderId != userId) throw new UnauthorizedAccessException("Unauthorized access");
        if (asset.ProcessingStatus != MediaProcessingStatus.PendingUpload)
            throw new ArgumentException($"Upload cannot be completed while status is {asset.ProcessingStatus}.");

        if (!await _mediaStorage.ObjectExistsAsync(asset.OriginalObjectKey))
            throw new ArgumentException("Uploaded file was not found in storage.");

        asset.MarkProcessing();
        await _repo.SaveChangesAsync();

        var targetMaxBytes = _limitPolicy.GetStorageLimits(asset.IntendedContext, asset.Kind).PerFileBytes;
        await _workQueue.EnqueueAsync(
            WorkQueueStreams.MediaUploaded,
            new MediaUploadedJob(
                asset.Id,
                asset.IntendedContext.ToString(),
                asset.Kind.ToString(),
                asset.MimeType,
                asset.OriginalObjectKey,
                asset.OriginalSizeBytes,
                targetMaxBytes));

        return MapToDto(asset);
    }

    public async Task ReportProcessedAsync(long id, MediaProcessedRequestDto request)
    {
        var asset = await GetMediaAssetOrThrowAsync(id);
        if (asset.ProcessingStatus != MediaProcessingStatus.Processing)
            throw new ArgumentException($"Media asset is not processing (status: {asset.ProcessingStatus}).");

        if (!string.IsNullOrWhiteSpace(request.FailureReason))
        {
            asset.MarkFailed(request.FailureReason.Trim());
            await _repo.SaveChangesAsync();
            return;
        }

        if (string.IsNullOrWhiteSpace(request.ProcessedObjectKey))
            throw new ArgumentException("ProcessedObjectKey is required when reporting success.");
        if (request.StoredSizeBytes is not > 0)
            throw new ArgumentException("StoredSizeBytes must be greater than zero when reporting success.");

        var storageLimit = _limitPolicy.GetStorageLimits(asset.IntendedContext, asset.Kind).PerFileBytes;
        if (request.StoredSizeBytes > storageLimit)
            throw new ArgumentException("Stored size exceeds the configured storage limit.");

        asset.MarkReady(request.ProcessedObjectKey.Trim(), request.StoredSizeBytes.Value);
        await _repo.SaveChangesAsync();
    }

    internal static string BuildObjectKey(long userId, string fileName)
    {
        var safeName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeName)) safeName = "upload.bin";

        return $"raw/{userId}/{Guid.NewGuid():N}/{safeName}";
    }

    private void EnsureMediaEnabled()
    {
        if (!_mediaOptions.Enabled) throw new InvalidOperationException("Media uploads are disabled.");
    }

    private async Task<MediaAsset> GetMediaAssetOrThrowAsync(long id) =>
        await _repo.GetMediaAssetByIdAsync(id) ?? throw new EntityNotFoundException("Media asset not found");

    private async Task<MediaAsset> GetOwnedOrAccessibleAssetAsync(long id)
    {
        var userId = GetUserIdFromLogin();
        var asset = await GetMediaAssetOrThrowAsync(id);
        if (asset.UploaderId == userId) return asset;

        throw new UnauthorizedAccessException("Unauthorized access");
    }

    private static MediaAssetGetResponseDto MapToDto(MediaAsset asset) =>
        new(
            asset.Id,
            asset.Kind,
            asset.IntendedContext,
            asset.ProcessingStatus,
            asset.MimeType,
            asset.OriginalFileName,
            asset.OriginalSizeBytes,
            asset.StoredSizeBytes,
            asset.FailureReason,
            asset.PostId,
            asset.CommentId,
            asset.ChatMessageId,
            asset.CreatedAt,
            asset.UpdatedAt);
}
