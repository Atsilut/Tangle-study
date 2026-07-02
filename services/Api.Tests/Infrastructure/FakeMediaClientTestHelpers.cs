using Api.Client;

namespace Api.Tests.Infrastructure;

internal static class FakeMediaClientTestHelpers
{
    public static long SeedReadyAsset(
        FakeMediaClient mediaClient,
        MediaIntendedContext context,
        string mimeType,
        string fileName,
        long storedSizeBytes) =>
        mediaClient.SeedReadyAsset(context, mimeType, fileName, storedSizeBytes);
}
