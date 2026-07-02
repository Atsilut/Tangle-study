using Api.Client;

namespace Api.Tests.Infrastructure;

internal static class MediaIntegrationTestHelpers
{
    public const long PostVideoPerFileBytes = 2_147_483_648;
    public const int IngressMultiplier = 3;

    public static long SeedReadyAsset(
        FakeMediaClient mediaClient,
        MediaIntendedContext context,
        string mimeType,
        string fileName,
        long storedSizeBytes) =>
        mediaClient.SeedReadyAsset(context, mimeType, fileName, storedSizeBytes);
}
