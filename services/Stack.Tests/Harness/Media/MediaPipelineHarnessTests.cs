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
