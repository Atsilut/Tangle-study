using Api.Domain.Chat.Service;
using Api.Domain.Comments.Service;
using Api.Domain.Posts.Service;
using Api.Domain.Users.Service;
using Api.Global.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Global.Api;

[ApiController]
[Route("internal/access")]
[Authorize]
[ServiceFilter(typeof(InternalAccessAuthorizationFilter))]
public sealed class InternalAccessController(
    UserService userService,
    PostService postService,
    CommentService commentService,
    ChatMessageService chatMessageService) : ControllerBase
{
    [HttpPost("users/{userId:long}/exists")]
    public async Task<IActionResult> EnsureUserExists([FromRoute] long userId)
    {
        await userService.EnsureUserExistsAsync(userId, "Authentication failed", StatusCodes.Status400BadRequest);
        return NoContent();
    }

    [HttpPost("posts/{postId:long}/media-view")]
    public async Task<IActionResult> EnsureCanViewPostMedia([FromRoute] long postId)
    {
        await postService.EnsureCanViewPostMediaAsync(postId);
        return NoContent();
    }

    [HttpPost("comments/{commentId:long}/media-view")]
    public async Task<IActionResult> EnsureCanViewCommentMedia([FromRoute] long commentId)
    {
        await commentService.EnsureCanViewCommentMediaAsync(commentId);
        return NoContent();
    }

    [HttpPost("chat-messages/{chatMessageId:long}/media-view")]
    public async Task<IActionResult> EnsureCanViewChatMessageMedia([FromRoute] long chatMessageId)
    {
        await chatMessageService.EnsureCurrentUserCanAccessMessageAsync(chatMessageId);
        return NoContent();
    }
}
