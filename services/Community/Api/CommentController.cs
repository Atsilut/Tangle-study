using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Community.Dto;
using Community.Service;
using Swashbuckle.AspNetCore.Annotations;

namespace Community.Api;

[ApiController]
[Route("api/comments")]
public class CommentController(CommentService service) : ControllerBase
{
    private readonly CommentService _service = service;

    [HttpPost]
    [Authorize]
    [SwaggerOperation(Summary = "Create Comment")]
    public async Task<IActionResult> CreateComment([FromBody] CommentCreateRequestDto request)
    {
        await _service.CreateCommentAsync(request);
        return Created();
    }

    [HttpGet("{id:long}")]
    [SwaggerOperation(Summary = "Get Comment By Id")]
    public async Task<ActionResult<CommentGetResponseDto>?> GetCommentById(long id)
    {
        var result = await _service.GetCommentByIdAsync(id);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpGet("post/{postId:long}")]
    [SwaggerOperation(Summary = "Get Comments By Post Id")]
    public async Task<ActionResult<List<CommentGetResponseDto>?>> GetCommentsByPostId(long postId)
    {
        var result = await _service.GetCommentsByPostIdAsync(postId);
        if (result == null) return NoContent();
        return Ok(result);
    }

    [HttpGet("user/{userId:long}")]
    [SwaggerOperation(Summary = "Get Comments By User Id")]
    public async Task<ActionResult<List<CommentGetResponseDto>?>> GetCommentsByUserId(long userId)
    {
        var result = await _service.GetCommentsByUserIdAsync(userId);
        if (result == null) return NoContent();
        return Ok(result);
    }

    [HttpPatch]
    [Authorize]
    [SwaggerOperation(Summary = "Update Comment")]
    public async Task<ActionResult<CommentPatchResponseDto>> UpdateComment([FromBody] CommentPatchRequestDto request)
    {
        var result = await _service.UpdateCommentAsync(request);
        return Ok(result);
    }

    [HttpDelete("{id:long}")]
    [Authorize]
    [SwaggerOperation(Summary = "Delete Comment")]
    public async Task<IActionResult> DeleteComment(long id)
    {
        await _service.DeleteCommentAsync(id);
        return NoContent();
    }
}
