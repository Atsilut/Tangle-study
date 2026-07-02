using Media.Dto;
using Media.Service;
using Media.Global.Security;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Media.Api;

[ApiController]
[Route("internal/media")]
public sealed class InternalMediaController(MediaService service) : ControllerBase
{
    private readonly MediaService _service = service;

    [HttpPatch("{id:long}/processed")]
    [ServiceFilter(typeof(WorkerCallbackAuthorizationFilter))]
    [SwaggerOperation(Summary = "Worker callback when media processing finishes")]
    public async Task<IActionResult> ReportProcessed(
        [FromRoute] long id,
        [FromBody] MediaProcessedRequestDto request)
    {
        await _service.ReportProcessedAsync(id, request);
        return NoContent();
    }

    [HttpPost("link/post")]
    [ServiceFilter(typeof(InternalServiceAuthorizationFilter))]
    public async Task<IActionResult> LinkPost([FromBody] LinkPostMediaRequestDto request)
    {
        await _service.LinkToPostAsync(request.PostId, request.UploaderUserId, request.MediaAssetIds);
        return NoContent();
    }

    [HttpPost("link/post/patch")]
    [ServiceFilter(typeof(InternalServiceAuthorizationFilter))]
    public async Task<IActionResult> PatchPostMedia([FromBody] PatchPostMediaRequestDto request)
    {
        await _service.PatchPostMediaAsync(
            request.PostId,
            request.UploaderUserId,
            request.AddMediaAssetIds,
            request.RemoveMediaAssetIds);
        return NoContent();
    }

    [HttpPost("link/comment")]
    [ServiceFilter(typeof(InternalServiceAuthorizationFilter))]
    public async Task<IActionResult> LinkComment([FromBody] LinkCommentMediaRequestDto request)
    {
        await _service.LinkToCommentAsync(request.CommentId, request.UploaderUserId, request.MediaAssetId);
        return NoContent();
    }

    [HttpPost("link/chat-message")]
    [ServiceFilter(typeof(InternalServiceAuthorizationFilter))]
    public async Task<IActionResult> LinkChatMessage([FromBody] LinkChatMessageMediaRequestDto request)
    {
        await _service.LinkToChatMessageAsync(
            request.ChatMessageId,
            request.SenderUserId,
            request.MediaAssetId);
        return NoContent();
    }

    [HttpPost("batch/by-post-ids")]
    [ServiceFilter(typeof(InternalServiceAuthorizationFilter))]
    public async Task<ActionResult<Dictionary<long, List<MediaAssetGetResponseDto>>>> GetByPostIds(
        [FromBody] BatchPostIdsRequestDto request) =>
        Ok(await _service.GetMediaByPostIdsAsync(request.PostIds));

    [HttpPost("batch/by-comment-ids")]
    [ServiceFilter(typeof(InternalServiceAuthorizationFilter))]
    public async Task<ActionResult<Dictionary<long, MediaAssetGetResponseDto?>>> GetByCommentIds(
        [FromBody] BatchCommentIdsRequestDto request) =>
        Ok(await _service.GetMediaByCommentIdsAsync(request.CommentIds));

    [HttpPost("batch/by-chat-message-ids")]
    [ServiceFilter(typeof(InternalServiceAuthorizationFilter))]
    public async Task<ActionResult<Dictionary<long, MediaAssetGetResponseDto?>>> GetByChatMessageIds(
        [FromBody] BatchChatMessageIdsRequestDto request) =>
        Ok(await _service.GetMediaByChatMessageIdsAsync(request.ChatMessageIds));

    [HttpDelete("for-post/{postId:long}")]
    [ServiceFilter(typeof(InternalServiceAuthorizationFilter))]
    public async Task<IActionResult> DeleteForPost([FromRoute] long postId)
    {
        await _service.DeleteBlobStorageForPostAsync(postId);
        return NoContent();
    }

    [HttpDelete("for-posts")]
    [ServiceFilter(typeof(InternalServiceAuthorizationFilter))]
    public async Task<IActionResult> DeleteForPosts([FromBody] DeletePostsMediaRequestDto request)
    {
        await _service.DeleteBlobStorageForPostsAsync(request.PostIds);
        return NoContent();
    }

    [HttpDelete("for-comment/{commentId:long}")]
    [ServiceFilter(typeof(InternalServiceAuthorizationFilter))]
    public async Task<IActionResult> DeleteForComment([FromRoute] long commentId)
    {
        await _service.DeleteBlobStorageForCommentAsync(commentId);
        return NoContent();
    }

    [HttpDelete("for-comments")]
    [ServiceFilter(typeof(InternalServiceAuthorizationFilter))]
    public async Task<IActionResult> DeleteForComments([FromBody] DeleteCommentsMediaRequestDto request)
    {
        await _service.DeleteBlobStorageForCommentsAsync(request.CommentIds);
        return NoContent();
    }

    [HttpDelete("for-chat-message/{chatMessageId:long}")]
    [ServiceFilter(typeof(InternalServiceAuthorizationFilter))]
    public async Task<IActionResult> DeleteForChatMessage([FromRoute] long chatMessageId)
    {
        await _service.DeleteBlobStorageForChatMessageAsync(chatMessageId);
        return NoContent();
    }

    [HttpPost("detach-uploader/{userId:long}")]
    [ServiceFilter(typeof(InternalServiceAuthorizationFilter))]
    public async Task<IActionResult> DetachUploader([FromRoute] long userId)
    {
        await _service.DetachUploaderFromDeletedUserAsync(userId);
        return NoContent();
    }
}
