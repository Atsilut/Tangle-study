using Stack.Tests.Infrastructure;
using Tangle.TestSupport.Auth;
using Tangle.TestSupport.Harness;
using Users.Dto;

namespace Stack.Tests.Harness.Community;

[Collection(HarnessTestCollection.Name)]
[Trait(HarnessTraits.Category, HarnessTraits.Harness)]
[Trait(HarnessTraits.HarnessModule, HarnessTraits.Community)]
public sealed class PostCommentHarnessTests : HarnessTestBase
{
    [Fact]
    public async Task CreatePost_ListAndComment_ThroughGateway()
    {
        const string testMethodName = nameof(CreatePost_ListAndComment_ThroughGateway);

        var author = await HarnessAuthHelpers.CreateUserForTestAsync(Client, UniqueTestPrefix(testMethodName));
        await HarnessAuthHelpers.LoginAsAsync(Client, author);

        var post = await CommunityHarnessHelpers.CreatePostAsync(Client, author, "Harness post", "Body text");
        var posts = await CommunityHarnessHelpers.ListPostsAsync(Client, author);
        Assert.Contains(posts, p => p.Id == post.Id && p.Title == "Harness post");

        var comment = await CommunityHarnessHelpers.CreateCommentAsync(Client, author, post.Id, "Nice post");
        var comments = await CommunityHarnessHelpers.ListCommentsByPostAsync(Client, author, post.Id);
        var saved = Assert.Single(comments);
        Assert.Equal(comment.Content, saved.Content);
        Assert.Equal(post.Id, saved.PostId);
        Assert.Equal(author.Id, saved.AuthorId);
    }
}
