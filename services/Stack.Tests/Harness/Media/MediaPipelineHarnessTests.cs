using Media.Entities;
using Stack.Tests.Infrastructure;
using Tangle.TestSupport.Auth;
using Tangle.TestSupport.Harness;
using Users.Dto;

namespace Stack.Tests.Harness.Media;

[Collection(HarnessTestCollection.Name)]
[Trait(HarnessTraits.Category, HarnessTraits.Harness)]
[Trait(HarnessTraits.HarnessModule, HarnessTraits.Media)]
public sealed class MediaPipelineHarnessTests : HarnessTestBase
{
    [Fact]
    public async Task ImageUpload_ProcessesToReady()
    {
        const string testMethodName = nameof(ImageUpload_ProcessesToReady);

        UserGetResponseDto user = await HarnessAuthHelpers.CreateUserForTestAsync(Client, UniqueTestPrefix(testMethodName));
        await HarnessAuthHelpers.LoginAsAsync(Client, user);

        var asset = await MediaHarnessHelpers.UploadFixtureThroughPipelineAsync(
            Client,
            fixtureFileName: "sample.jpg",
            mimeType: "image/jpeg",
            context: MediaIntendedContext.Post,
            // worker-media bounds a single attempt to 45s (JOB_TIMEOUT) and can retry
            // after a short backoff, so a lone slow/cold-start attempt plus one retry
            // can approach ~90s; keep this comfortably above that instead of 60s.
            processingTimeout: TimeSpan.FromSeconds(90));

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

        var user = await HarnessAuthHelpers.CreateUserForTestAsync(Client, UniqueTestPrefix(testMethodName));
        await HarnessAuthHelpers.LoginAsAsync(Client, user);

        var asset = await MediaHarnessHelpers.UploadFixtureThroughPipelineAsync(
            Client,
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
}
