using Media.Config;
using Media.Entities;
using Microsoft.Extensions.Options;

namespace Media;

public sealed class MediaLimitPolicy(IOptions<MediaOptions> options)
{
    private readonly MediaOptions _options = options.Value;

    public MediaKind ClassifyKind(string mimeType)
    {
        if (string.IsNullOrWhiteSpace(mimeType)) throw new ArgumentException("MIME type is required.");

        if (mimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)) return MediaKind.Video;

        if (mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return MediaKind.Image;

        throw new ArgumentException($"Unsupported media MIME type: {mimeType}");
    }

    public MediaStorageLimits GetStorageLimits(MediaIntendedContext context, MediaKind kind)
    {
        var limits = GetContextLimits(context);
        return kind switch
        {
            MediaKind.Video => new MediaStorageLimits(limits.VideoPerFileBytes, limits.VideoTotalBytes),
            MediaKind.Image => new MediaStorageLimits(limits.ImagePerFileBytes, limits.ImageTotalBytes),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown media kind."),
        };
    }

    public long GetIngressLimit(MediaIntendedContext context, MediaKind kind) =>
        checked((long)(GetStorageLimits(context, kind).PerFileBytes * GetIngressMultiplier()));

    public double GetIngressMultiplier() => _options.IngressMultiplier;

    public void EnsureWithinIngressLimit(MediaIntendedContext context, MediaKind kind, long declaredSizeBytes)
    {
        if (declaredSizeBytes <= 0) throw new ArgumentException("File size must be greater than zero.");

        var ingressLimit = GetIngressLimit(context, kind);
        if (declaredSizeBytes > ingressLimit)
            throw new ArgumentException(
                $"File size {declaredSizeBytes} bytes exceeds the upload limit of {ingressLimit} bytes for {context} {kind}.");
    }

    public bool AllowsMultipleFiles(MediaIntendedContext context) => context == MediaIntendedContext.Post;

    private MediaContextLimitOptions GetContextLimits(MediaIntendedContext context) =>
        context switch
        {
            MediaIntendedContext.Post => _options.Post,
            MediaIntendedContext.Comment => _options.Comment,
            MediaIntendedContext.ChatMessage => _options.ChatMessage,
            _ => throw new ArgumentOutOfRangeException(nameof(context), context, "Unknown media context."),
        };
}

public readonly record struct MediaStorageLimits(long PerFileBytes, long? TotalBytes);
