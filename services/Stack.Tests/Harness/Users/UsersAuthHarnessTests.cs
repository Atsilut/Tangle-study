using System.Net;
using System.Net.Http.Json;
using Stack.Tests.Infrastructure;
using Tangle.TestSupport.Auth;
using Tangle.TestSupport.Harness;
using Users.Dto;

namespace Stack.Tests.Harness.Users;

[Collection(HarnessTestCollection.Name)]
[Trait(HarnessTraits.Category, HarnessTraits.Harness)]
[Trait(HarnessTraits.HarnessModule, HarnessTraits.Users)]
public sealed class UsersAuthHarnessTests : HarnessTestBase
{
    [Fact]
    public async Task JoinLogin_GetAndPatchProfile_ThroughGateway()
    {
        const string testMethodName = nameof(JoinLogin_GetAndPatchProfile_ThroughGateway);
        const string updatedNickname = "HarnessNick";

        var user = await HarnessAuthHelpers.CreateUserForTestAsync(Client, UniqueTestPrefix(testMethodName));
        await HarnessAuthHelpers.LoginAsAsync(Client, user);

        var getRes = await Client.GetAsync($"/api/users/{user.Id}", TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(getRes, HttpStatusCode.OK);
        var profile = await getRes.Content.ReadFromJsonAsync<UserGetResponseDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(profile);
        Assert.Equal(user.Nickname, profile.Nickname);

        var patchRes = await Client.PatchAsJsonAsync(
            "/api/users",
            new UserPatchRequestDto { Id = user.Id, Nickname = updatedNickname },
            TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(patchRes, HttpStatusCode.OK);

        var getAfterRes = await Client.GetAsync($"/api/users/{user.Id}", TestContext.Current.CancellationToken);
        var updated = await getAfterRes.Content.ReadFromJsonAsync<UserGetResponseDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal(updatedNickname, updated.Nickname);
    }
}
