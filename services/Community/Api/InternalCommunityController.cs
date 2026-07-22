using Microsoft.AspNetCore.Mvc;
using Tangle.AspNetCore.Security;
using Community.Service;

namespace Community.Api;

[ApiController]
[Route("internal/community")]
[ServiceFilter(typeof(InternalAccessAuthorizationFilter))]
public sealed class InternalCommunityController(PostService postService, CommentService commentService) : ControllerBase
{
    [HttpPost("posts/{postId:long}/exists")]
    public async Task<IActionResult> PostExists([FromRoute] long postId)
    {
        if (!await postService.PostExistsAsync(postId)) return NotFound();
        return NoContent();
    }

    [HttpPost("comments/{commentId:long}/exists")]
    public async Task<IActionResult> CommentExists([FromRoute] long commentId)
    {
        if (!await commentService.CommentExistsAsync(commentId)) return NotFound();
        return NoContent();
    }

    [HttpPost("{postId:long}/media-view")]
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

    [HttpPost("{postId:long}/validate-owner")]
    public async Task<IActionResult> ValidatePostOwner([FromRoute] long postId)
    {
        await postService.EnsureCallerOwnsPostAsync(postId);
        return NoContent();
    }

    [HttpPost("viewable-ids")]
    public async Task<ActionResult<InternalCommunityViewableIdsResponseDto>> GetViewablePostIds(
        [FromBody] InternalCommunityViewableIdsRequestDto request)
    {
        var viewable = await postService.GetViewablePostIdsAsync(request.PostIds, request.ViewerUserId);
        return Ok(new InternalCommunityViewableIdsResponseDto([.. viewable]));
    }

    [HttpPost("users/{userId:long}/detach-on-deletion")]
    public async Task<IActionResult> DetachOnDeletion([FromRoute] long userId)
    {
        await postService.DetachAuthorFromDeletedUserAsync(userId);
        await commentService.DetachAuthorFromDeletedUserAsync(userId);
        return NoContent();
    }

    [HttpPost("groups/{groupId:long}/delete-all")]
    public async Task<IActionResult> DeleteAllByGroup([FromRoute] long groupId)
    {
        await postService.DeleteAllByGroupAsync(groupId);
        return NoContent();
    }
}

public sealed record InternalCommunityViewableIdsRequestDto(long[] PostIds, long? ViewerUserId);

public sealed record InternalCommunityViewableIdsResponseDto(long[] ViewablePostIds);
