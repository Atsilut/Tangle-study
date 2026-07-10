using Media.Client;
using Media.Dto;
using Media.Repository;
using Media.Storage;
using Media.Config;
using Tangle.AspNetCore.Exceptions;
using Media.Infrastructure;
using Media.Queue;
using Microsoft.Extensions.Options;
using Tangle.AspNetCore.Auth;
using Tangle.AspNetCore.Queue;
using Media.Entities;

namespace Media.Service;

[Service]
public sealed class MediaService(
    IMediaAssetRepository repo,
    IServiceProvider serviceProvider,
    MediaLimitPolicy limitPolicy,
    IUserClient userClient,
    ICommunityAccessClient communityAccess,
    IChatAccessClient chatAccess,
    IWorkQueue workQueue,
    IOptions<MediaOptions> mediaOptions,
    CurrentUserAccessor currentUser)
{
    private static readonly TimeSpan PresignedUploadExpiry = TimeSpan.FromHours(1);

    private readonly IMediaAssetRepository _repo = repo;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly MediaLimitPolicy _limitPolicy = limitPolicy;
    private readonly IUserClient _userClient = userClient;
    private readonly ICommunityAccessClient _communityAccess = communityAccess;
    private readonly IChatAccessClient _chatAccess = chatAccess;
    private readonly IWorkQueue _workQueue = workQueue;
    private readonly MediaOptions _mediaOptions = mediaOptions.Value;
    private readonly CurrentUserAccessor _currentUser = currentUser;

    private long GetUserIdFromLogin() => _currentUser.GetUserIdFromLogin();

    public async Task<MediaAssetGetResponseDto> GetMediaAssetByIdAsync(long id)
    {
        var asset = await GetOwnedOrAccessibleAssetAsync(id);
        return MapToDto(asset);
    }

    public async Task<MediaContentResult> GetContentAsync(long id)
    {
        var asset = await GetMediaAssetOrThrowAsync(id);
        await EnsureCanReadContentAsync(asset);

        var objectKey = asset.ProcessedObjectKey
            ?? throw new InvalidOperationException("Processed media object is missing.");
        var stream = await RequireMediaStorage().OpenReadAsync(objectKey);
        return new MediaContentResult(stream, asset.MimeType, asset.OriginalFileName);
    }

    public async Task DeleteUnlinkedMediaAssetByIdAsync(long id)
    {
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

        var assets = await _repo.GetMediaAssetsByPostIdsAsync(postIds);
        await DeleteBlobStorageForAssetsAsync(assets);
    }

    internal async Task DeleteBlobStorageForCommentAsync(long commentId)
    {
        var asset = await _repo.GetMediaAssetByCommentIdAsync(commentId);
        if (asset is not null) await DeleteBlobStorageForAssetsAsync([asset]);
    }

    internal async Task DeleteBlobStorageForCommentsAsync(IReadOnlyCollection<long> commentIds)
    {
        var assetsByCommentId = await _repo.GetMediaAssetByCommentIdsAsync(commentIds);
        var assets = assetsByCommentId.Values.Where(asset => asset is not null).Cast<MediaAsset>().ToList();
        await DeleteBlobStorageForAssetsAsync(assets);
    }

    internal async Task DeleteBlobStorageForChatMessageAsync(long chatMessageId)
    {
        var asset = await _repo.GetMediaAssetByChatMessageIdAsync(chatMessageId);
        if (asset is not null) await DeleteBlobStorageForAssetsAsync([asset]);
    }

    internal Task DetachUploaderFromDeletedUserAsync(long uploaderId) =>
        _repo.DetachUploaderFromMediaAssetsAsync(uploaderId);

    internal async Task LinkToPostAsync(long postId, long uploaderUserId, IReadOnlyList<long>? mediaAssetIds)
    {
        if (mediaAssetIds is null || mediaAssetIds.Count == 0) return;

        if (mediaAssetIds.Distinct().Count() != mediaAssetIds.Count)
            throw new ArgumentException("Duplicate media asset IDs are not allowed.");

        var assets = await LoadOwnedReadyAssetsAsync(mediaAssetIds, uploaderUserId, MediaIntendedContext.Post);
        ValidatePostTotalsMedia(assets);

        foreach (var asset in assets)
            asset.LinkToPost(postId);

        await _repo.SaveChangesAsync();
    }

    internal async Task PatchPostMediaAsync(
        long postId,
        long uploaderUserId,
        IReadOnlyList<long>? addMediaAssetIds,
        IReadOnlyList<long>? removeMediaAssetIds)
    {
        List<long> addIds = addMediaAssetIds is null ? [] : [.. addMediaAssetIds];
        List<long> removeIds = removeMediaAssetIds is null ? [] : [.. removeMediaAssetIds];
        if (addIds.Count == 0 && removeIds.Count == 0) return;

        if (addIds.Distinct().Count() != addIds.Count || removeIds.Distinct().Count() != removeIds.Count)
            throw new ArgumentException("Duplicate media asset IDs are not allowed.");
        if (addIds.Intersect(removeIds).Any())
            throw new ArgumentException("Cannot add and remove the same media asset in one request.");

        var current = await _repo.GetMediaAssetsByPostIdAsync(postId);
        List<MediaAsset> toRemove = [];
        foreach (var removeId in removeIds)
        {
            var asset = current.FirstOrDefault(a => a.Id == removeId)
                ?? throw new ArgumentException($"Media asset {removeId} is not attached to this post.");
            toRemove.Add(asset);
        }

        if (toRemove.Count > 0)
        {
            await DeleteBlobStorageForAssetsAsync(toRemove);
            foreach (var asset in toRemove)
                await _repo.DeleteMediaAssetAsync(asset);
        }

        var remaining = current.Where(a => !removeIds.Contains(a.Id)).ToList();
        var toAdd = addIds.Count == 0
            ? new List<MediaAsset>()
            : await LoadOwnedReadyAssetsAsync(addIds, uploaderUserId, MediaIntendedContext.Post);
        ValidatePostTotalsMedia([.. remaining, .. toAdd]);

        foreach (var asset in toAdd)
            asset.LinkToPost(postId);

        await _repo.SaveChangesAsync();
    }

    internal async Task LinkToCommentAsync(long commentId, long uploaderUserId, long? mediaAssetId)
    {
        if (mediaAssetId is null) return;

        var asset = await LoadSingleOwnedReadyAssetAsync(mediaAssetId.Value, uploaderUserId, MediaIntendedContext.Comment);
        asset.LinkToComment(commentId);
        await _repo.SaveChangesAsync();
    }

    internal async Task LinkToChatMessageAsync(long chatMessageId, long senderUserId, long? mediaAssetId)
    {
        if (mediaAssetId is null) return;

        var asset = await LoadSingleOwnedReadyAssetAsync(mediaAssetId.Value, senderUserId, MediaIntendedContext.ChatMessage);
        asset.LinkToChatMessage(chatMessageId);
        await _repo.SaveChangesAsync();
    }

    internal async Task<IReadOnlyList<MediaAssetGetResponseDto>> GetMediaForPostAsync(long postId)
    {
        var assets = await _repo.GetMediaAssetsByPostIdAsync(postId);
        return [.. assets.Select(MapToDto)];
    }

    internal async Task<IReadOnlyDictionary<long, IReadOnlyList<MediaAssetGetResponseDto>>> GetMediaByPostIdsAsync(
        IReadOnlyCollection<long> postIds)
    {
        if (postIds.Count == 0) return new Dictionary<long, IReadOnlyList<MediaAssetGetResponseDto>>();

        var assets = await _repo.GetMediaAssetsByPostIdsAsync(postIds);
        return assets
            .GroupBy(asset => asset.PostId!.Value)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<MediaAssetGetResponseDto>)[.. group.Select(MapToDto)]);
    }

    internal async Task<MediaAssetGetResponseDto?> GetMediaForCommentAsync(long commentId)
    {
        var asset = await _repo.GetMediaAssetByCommentIdAsync(commentId);
        return asset is null ? null : MapToDto(asset);
    }

    internal async Task<IReadOnlyDictionary<long, MediaAssetGetResponseDto?>> GetMediaByCommentIdsAsync(
        IReadOnlyCollection<long> commentIds)
    {
        if (commentIds.Count == 0) return new Dictionary<long, MediaAssetGetResponseDto?>();

        var assetsByCommentId = await _repo.GetMediaAssetByCommentIdsAsync(commentIds);
        return assetsByCommentId.ToDictionary(
            pair => pair.Key,
            pair => pair.Value is null ? null : MapToDto(pair.Value));
    }

    internal async Task<MediaAssetGetResponseDto?> GetMediaForChatMessageAsync(long chatMessageId)
    {
        var asset = await _repo.GetMediaAssetByChatMessageIdAsync(chatMessageId);
        return asset is null ? null : MapToDto(asset);
    }

    internal async Task<IReadOnlyDictionary<long, MediaAssetGetResponseDto?>> GetMediaByChatMessageIdsAsync(
        IReadOnlyCollection<long> chatMessageIds)
    {
        if (chatMessageIds.Count == 0) return new Dictionary<long, MediaAssetGetResponseDto?>();

        var assetsByMessageId = await _repo.GetMediaAssetByChatMessageIdsAsync(chatMessageIds);
        return assetsByMessageId.ToDictionary(
            pair => pair.Key,
            pair => pair.Value is null ? null : MapToDto(pair.Value));
    }

    private const int MaxConcurrentBlobDeletes = 8;

    private async Task DeleteBlobStorageForAssetsAsync(IReadOnlyList<MediaAsset> assets)
    {
        if (_serviceProvider.GetService<IMediaStorage>() is not IMediaStorage mediaStorage)
            return;

        await Parallel.ForEachAsync(
            assets,
            new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrentBlobDeletes },
            async (asset, cancellationToken) =>
                await DeleteBlobStorageForAssetAsync(mediaStorage, asset, cancellationToken));
    }

    private static async Task DeleteBlobStorageForAssetAsync(
        IMediaStorage mediaStorage,
        MediaAsset asset,
        CancellationToken cancellationToken)
    {
        if (await mediaStorage.ObjectExistsAsync(asset.OriginalObjectKey, cancellationToken))
            await mediaStorage.DeleteObjectAsync(asset.OriginalObjectKey, cancellationToken);

        if (!string.IsNullOrWhiteSpace(asset.ProcessedObjectKey)
            && await mediaStorage.ObjectExistsAsync(asset.ProcessedObjectKey, cancellationToken))
            await mediaStorage.DeleteObjectAsync(asset.ProcessedObjectKey, cancellationToken);
    }

    public async Task<MediaUploadInitResponseDto> InitUploadAsync(MediaUploadInitRequestDto request)
    {
        var userId = GetUserIdFromLogin();
        await _userClient.EnsureUserExistsAsync(userId);

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

        var presigned = await RequireMediaStorage().CreatePresignedUploadAsync(
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
        var userId = GetUserIdFromLogin();
        var asset = await GetMediaAssetOrThrowAsync(id);
        if (asset.UploaderId != userId) throw new UnauthorizedAccessException("Unauthorized access");
        if (asset.ProcessingStatus != MediaProcessingStatus.PendingUpload)
            throw new ArgumentException($"Upload cannot be completed while status is {asset.ProcessingStatus}.");

        if (!await RequireMediaStorage().ObjectExistsAsync(asset.OriginalObjectKey))
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

    internal async Task ReportProcessedAsync(long id, MediaProcessedRequestDto request)
    {
        var asset = await GetMediaAssetOrThrowAsync(id);
        if (asset.ProcessingStatus == MediaProcessingStatus.Ready)
        {
            if (!string.IsNullOrWhiteSpace(request.FailureReason))
                throw new ArgumentException("Cannot mark a ready media asset as failed.");
            return;
        }

        if (asset.ProcessingStatus == MediaProcessingStatus.Failed)
        {
            if (string.IsNullOrWhiteSpace(request.FailureReason))
                throw new ArgumentException("Cannot mark a failed media asset as ready.");
            return;
        }

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

    private IMediaStorage RequireMediaStorage() =>
        _serviceProvider.GetService<IMediaStorage>()
            ?? throw new InvalidOperationException(
                "Media:ConnectionString is not configured. " +
                "Start Azurite (docker compose up azurite) and set the connection string.");

    private async Task<MediaAsset> GetMediaAssetOrThrowAsync(long id) =>
        await _repo.GetMediaAssetByIdAsync(id) ?? throw new EntityNotFoundException("Media asset not found");

    private async Task<MediaAsset> GetOwnedOrAccessibleAssetAsync(long id)
    {
        var userId = GetUserIdFromLogin();
        var asset = await GetMediaAssetOrThrowAsync(id);
        if (asset.UploaderId == userId) return asset;

        throw new UnauthorizedAccessException("Unauthorized access");
    }

    private async Task EnsureCanReadContentAsync(MediaAsset asset)
    {
        if (asset.ProcessingStatus != MediaProcessingStatus.Ready)
            throw new ArgumentException("Media is not ready for viewing.");
        if (string.IsNullOrWhiteSpace(asset.ProcessedObjectKey))
            throw new InvalidOperationException("Processed media object is missing.");

        if (asset.PostId is long postId)
        {
            await _communityAccess.EnsureCanViewPostMediaAsync(postId);
            return;
        }

        if (asset.CommentId is long commentId)
        {
            await _communityAccess.EnsureCanViewCommentMediaAsync(commentId);
            return;
        }

        if (asset.ChatMessageId is long messageId)
        {
            await _chatAccess.EnsureCanAccessChatMessageMediaAsync(messageId);
            return;
        }

        var userId = GetUserIdFromLogin();
        if (asset.UploaderId != userId) throw new UnauthorizedAccessException("Unauthorized access");
    }

    private async Task<List<MediaAsset>> LoadOwnedReadyAssetsAsync(
        IReadOnlyList<long> mediaAssetIds,
        long uploaderUserId,
        MediaIntendedContext expectedContext)
    {
        var assets = await _repo.GetMediaAssetsByIdsAsync(mediaAssetIds);
        if (assets.Count != mediaAssetIds.Count)
            throw new EntityNotFoundException("One or more media assets were not found");

        var assetsById = assets.ToDictionary(asset => asset.Id);
        var orderedAssets = new List<MediaAsset>(mediaAssetIds.Count);
        foreach (var id in mediaAssetIds)
            orderedAssets.Add(assetsById[id]);

        foreach (var asset in orderedAssets)
            EnsureOwnedReadyForContext(asset, uploaderUserId, expectedContext);

        return orderedAssets;
    }

    private async Task<MediaAsset> LoadSingleOwnedReadyAssetAsync(
        long mediaAssetId,
        long uploaderUserId,
        MediaIntendedContext expectedContext)
    {
        var asset = await GetMediaAssetOrThrowAsync(mediaAssetId);
        EnsureOwnedReadyForContext(asset, uploaderUserId, expectedContext);
        return asset;
    }

    private void EnsureOwnedReadyForContext(MediaAsset asset, long uploaderUserId, MediaIntendedContext expectedContext)
    {
        if (asset.UploaderId != uploaderUserId) throw new UnauthorizedAccessException("Unauthorized access");
        if (asset.IntendedContext != expectedContext)
            throw new ArgumentException($"Media asset {asset.Id} was uploaded for {asset.IntendedContext}, not {expectedContext}.");
        if (asset.IsLinked) throw new ArgumentException($"Media asset {asset.Id} is already linked to content.");
        if (asset.ProcessingStatus != MediaProcessingStatus.Ready)
            throw new ArgumentException($"Media asset {asset.Id} must be ready before it can be attached.");
        if (asset.StoredSizeBytes is not > 0)
            throw new ArgumentException($"Media asset {asset.Id} is missing processed size metadata.");

        var perFileLimit = _limitPolicy.GetStorageLimits(expectedContext, asset.Kind).PerFileBytes;
        if (asset.StoredSizeBytes > perFileLimit)
            throw new ArgumentException($"Media asset {asset.Id} exceeds the per-file storage limit for {expectedContext} {asset.Kind}.");
    }

    private void ValidatePostTotalsMedia(IReadOnlyList<MediaAsset> assets)
    {
        long videoTotal = 0;
        long imageTotal = 0;
        var videoLimits = _limitPolicy.GetStorageLimits(MediaIntendedContext.Post, MediaKind.Video);
        var imageLimits = _limitPolicy.GetStorageLimits(MediaIntendedContext.Post, MediaKind.Image);

        foreach (var asset in assets)
        {
            var size = asset.StoredSizeBytes!.Value;
            if (asset.Kind == MediaKind.Video)
                videoTotal = checked(videoTotal + size);
            else
                imageTotal = checked(imageTotal + size);
        }

        if (videoLimits.TotalBytes is not null && videoTotal > videoLimits.TotalBytes)
            throw new ArgumentException("Attached post videos exceed the configured total video storage limit.");
        if (imageLimits.TotalBytes is not null && imageTotal > imageLimits.TotalBytes)
            throw new ArgumentException("Attached post images exceed the configured total image storage limit.");
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
