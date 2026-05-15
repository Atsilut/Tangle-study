using Api.Domain.Comments.Dto;
using Api.Domain.Comments.Service;
using Api.Global.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Api.Domain.Comments.Api
{
    [ApiController]
    [Route("api/comments")]
    public class CommentController : ControllerBase
    {
        private readonly CommentService _service;

        public CommentController(CommentService service)
        {
            _service = service;
        }

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
            if (result == null || result.Count == 0) return NoContent();
            return Ok(result);
        }

        [HttpGet("user/{userId:long}")]
        [SwaggerOperation(Summary = "Get Comments By User Id")]
        public async Task<ActionResult<List<CommentGetResponseDto>?>> GetCommentsByUserId(long userId)
        {
            var result = await _service.GetCommentsByUserIdAsync(userId);
            if (result == null || result.Count == 0) return NoContent();
            return Ok(result);
        }

        [HttpPatch]
        [Authorize]
        [SwaggerOperation(Summary = "Update Comment")]
        public async Task<ActionResult<CommentPatchResponseDto>?> UpdateComment([FromBody] CommentPatchRequestDto request)
        {
            try
            {
                var result = await _service.UpdateCommentAsync(request);
                if (result == null) return NotFound();
                return Ok(result);
            }
            catch (EntityNotFoundException)
            {
                return NotFound();
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized();
            }
        }
    }
}
