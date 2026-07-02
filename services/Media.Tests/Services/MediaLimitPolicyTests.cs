using Media.Global.Config;
using Microsoft.Extensions.Options;

namespace Media.Tests.Services;

public sealed class MediaLimitPolicyTests
{
    private static MediaLimitPolicy CreatePolicy(double ingressMultiplier = 3) =>
        new(Options.Create(CreateTestMediaOptions(ingressMultiplier)));

    private static MediaOptions CreateTestMediaOptions(double ingressMultiplier = 3) => new()
    {
        IngressMultiplier = ingressMultiplier,
        Post = new MediaContextLimitOptions
        {
            VideoPerFileBytes = 2L * 1024 * 1024 * 1024,
            VideoTotalBytes = 10L * 1024 * 1024 * 1024,
            ImagePerFileBytes = 150L * 1024 * 1024,
            ImageTotalBytes = 3L * 1024 * 1024 * 1024,
        },
        Comment = new MediaContextLimitOptions
        {
            VideoPerFileBytes = 150L * 1024 * 1024,
            ImagePerFileBytes = 75L * 1024 * 1024,
        },
        ChatMessage = new MediaContextLimitOptions
        {
            VideoPerFileBytes = 150L * 1024 * 1024,
            ImagePerFileBytes = 75L * 1024 * 1024,
        },
    };

    [Theory]
    [InlineData("video/mp4", MediaKind.Video)]
    [InlineData("image/jpeg", MediaKind.Image)]
    [InlineData("image/gif", MediaKind.Image)]
    public void ClassifyKind_MapsMimeTypes(string mimeType, MediaKind expected) =>
        Assert.Equal(expected, CreatePolicy().ClassifyKind(mimeType));

    [Fact]
    public void GetStorageLimits_PostVideo_MatchesConfiguredCaps()
    {
        var policy = CreatePolicy();
        var limits = policy.GetStorageLimits(MediaIntendedContext.Post, MediaKind.Video);
        Assert.Equal(2L * 1024 * 1024 * 1024, limits.PerFileBytes);
        Assert.Equal(10L * 1024 * 1024 * 1024, limits.TotalBytes);
    }

    [Fact]
    public void GetStorageLimits_CommentImage_HasSingleFileCapOnly()
    {
        var policy = CreatePolicy();
        var limits = policy.GetStorageLimits(MediaIntendedContext.Comment, MediaKind.Image);
        Assert.Equal(75L * 1024 * 1024, limits.PerFileBytes);
        Assert.Null(limits.TotalBytes);
    }

    [Fact]
    public void GetIngressLimit_UsesConfiguredMultiplier()
    {
        var policy = CreatePolicy(ingressMultiplier: 4);
        var ingress = policy.GetIngressLimit(MediaIntendedContext.ChatMessage, MediaKind.Image);
        Assert.Equal(75L * 1024 * 1024 * 4, ingress);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void GetIngressMultiplier_UsesConfiguredValue(double ingressMultiplier) =>
        Assert.Equal(ingressMultiplier, CreatePolicy(ingressMultiplier).GetIngressMultiplier());

    [Fact]
    public void EnsureWithinIngressLimit_RejectsOversizedUpload()
    {
        var policy = CreatePolicy();
        var ingress = policy.GetIngressLimit(MediaIntendedContext.Post, MediaKind.Video);
        var ex = Assert.Throws<ArgumentException>(() =>
            policy.EnsureWithinIngressLimit(MediaIntendedContext.Post, MediaKind.Video, ingress + 1));
        Assert.Contains("upload limit", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AllowsMultipleFiles_OnlyForPosts()
    {
        var policy = CreatePolicy();
        Assert.True(policy.AllowsMultipleFiles(MediaIntendedContext.Post));
        Assert.False(policy.AllowsMultipleFiles(MediaIntendedContext.Comment));
        Assert.False(policy.AllowsMultipleFiles(MediaIntendedContext.ChatMessage));
    }
}
