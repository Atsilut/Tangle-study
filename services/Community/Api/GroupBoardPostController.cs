using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Community.Dto;
using Community.Service;
using Swashbuckle.AspNetCore.Annotations;

namespace Community.Api;

[ApiController]
[Route("api/groups/{groupId:long}/boards/{boardId:long}/posts")]
[Authorize]
public class GroupBoardPostController(PostService service) : ControllerBase
{
    private readonly PostService _service = service;

    [HttpPost]
    [SwaggerOperation(Summary = "Create a post on a group board")]
    public async Task<IActionResult> Create(
        [FromRoute] long groupId,
        [FromRoute] long boardId,
        [FromBody] GroupBoardPostCreateRequestDto request)
    {
        await _service.CreateGroupBoardPostAsync(groupId, boardId, request);
        return Created();
    }

    [HttpGet]
    [SwaggerOperation(Summary = "List posts on a group board")]
    public async Task<ActionResult<List<PostGetResponseDto>?>> List(
        [FromRoute] long groupId,
        [FromRoute] long boardId)
    {
        var result = await _service.GetGroupBoardPostsAsync(groupId, boardId);
        if (result == null) return NoContent();
        return Ok(result);
    }

    [HttpGet("{postId:long}")]
    [SwaggerOperation(Summary = "Get a post on a group board")]
    public async Task<ActionResult<PostGetResponseDto?>> GetById(
        [FromRoute] long groupId,
        [FromRoute] long boardId,
        [FromRoute] long postId)
    {
        var result = await _service.GetGroupBoardPostByIdAsync(groupId, boardId, postId);
        if (result == null) return NotFound();
        return Ok(result);
    }
}
