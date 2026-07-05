using Media;
using Stack.Tests.Infrastructure;

namespace Stack.Tests.Harness;

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

        var user = await HarnessAuthHelpers.CreateUserForTestAsync(_client!, UniqueTestPrefix(testMethodName));
        await HarnessAuthHelpers.LoginAsAsync(_client!, user);

        var asset = await MediaHarnessHelpers.UploadFixtureThroughPipelineAsync(
            _client!,
            fixtureFileName: "sample.jpg",
            mimeType: "image/jpeg",
            context: MediaIntendedContext.Post,
            processingTimeout: TimeSpan.FromSeconds(60));

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

        var user = await HarnessAuthHelpers.CreateUserForTestAsync(_client!, UniqueTestPrefix(testMethodName));
        await HarnessAuthHelpers.LoginAsAsync(_client!, user);

        var asset = await MediaHarnessHelpers.UploadFixtureThroughPipelineAsync(
            _client!,
            fixtureFileName: "sample.mp4",
            mimeType: "video/mp4",
            context: MediaIntendedContext.Post,
            processingTimeout: TimeSpan.FromSeconds(180));

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
