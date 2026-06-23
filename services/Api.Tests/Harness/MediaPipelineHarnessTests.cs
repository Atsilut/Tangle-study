using Api.Domain.Media.Domain;
using Api.Tests.Infrastructure;

namespace Api.Tests.Harness;

[Collection(MediaHarnessTestCollection.Name)]
[Trait("Category", "Harness")]
public sealed class MediaPipelineHarnessTests : IAsyncLifetime, IAsyncDisposable
{
    private HttpClient? _client;

    public async ValueTask InitializeAsync()
    {
        Assert.SkipUnless(
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(MediaHarnessHelpers.ApiBaseUrlEnv)),
            $"{MediaHarnessHelpers.ApiBaseUrlEnv} is not set.");

        _client = MediaHarnessHelpers.CreateHarnessClient();
        await MediaHarnessHelpers.WaitForApiReadyAsync(_client, TimeSpan.FromSeconds(90));
    }

    [Fact]
    public async Task ImageUpload_ProcessesToReady()
    {
        const string testMethodName = nameof(ImageUpload_ProcessesToReady);

        // Arrange
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(_client!, UniqueTestPrefix(testMethodName));
        await IntegrationTestAuthHelpers.LoginAsAsync(_client!, user);

        // Act
        var asset = await MediaHarnessHelpers.UploadFixtureThroughPipelineAsync(
            _client!,
            fixtureFileName: "sample.jpg",
            mimeType: "image/jpeg",
            context: MediaIntendedContext.Post,
            processingTimeout: TimeSpan.FromSeconds(60));

        // Assert
        Assert.Equal(MediaProcessingStatus.Ready, asset.ProcessingStatus);
        Assert.NotNull(asset.StoredSizeBytes);
        Assert.True(asset.StoredSizeBytes > 0);
        Assert.True(asset.StoredSizeBytes <= MediaHarnessHelpers.PostImagePerFileBytes);
        Assert.Null(asset.FailureReason);
    }

    [Fact]
    public async Task VideoUpload_ProcessesToReady()
    {
        const string testMethodName = nameof(VideoUpload_ProcessesToReady);

        // Arrange
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(_client!, UniqueTestPrefix(testMethodName));
        await IntegrationTestAuthHelpers.LoginAsAsync(_client!, user);

        // Act
        var asset = await MediaHarnessHelpers.UploadFixtureThroughPipelineAsync(
            _client!,
            fixtureFileName: "sample.mp4",
            mimeType: "video/mp4",
            context: MediaIntendedContext.Post,
            processingTimeout: TimeSpan.FromSeconds(120));

        // Assert
        Assert.Equal(MediaProcessingStatus.Ready, asset.ProcessingStatus);
        Assert.NotNull(asset.StoredSizeBytes);
        Assert.True(asset.StoredSizeBytes > 0);
        Assert.True(asset.StoredSizeBytes <= MediaHarnessHelpers.PostVideoPerFileBytes);
        Assert.Null(asset.FailureReason);
    }

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        return ValueTask.CompletedTask;
    }

    private static string UniqueTestPrefix(string testMethodName) =>
        $"{testMethodName}_{Guid.NewGuid():N}"[..Math.Min(40, testMethodName.Length + 9)];
}
