using Api.Domain.Posts.Dto;
using Api.Domain.Posts.Service;
using Api.Global.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Api.Domain.Posts.Api
{
    [ApiController]
    [Route("api/posts")]
    public class PostController : ControllerBase
    {
        private readonly PostService _service;

        public PostController(PostService service)
        {
            _service = service;
        }

        [HttpPost]
        [Authorize]
        [SwaggerOperation(Summary = "Create Post")]
        public async Task<IActionResult> CreatePost([FromBody] PostCreateRequestDto request)
        {
            await _service.CreatePostAsync(request);
            return Created();
        }

        [HttpGet]
        [SwaggerOperation(Summary = "Get All Posts")]
        public async Task<ActionResult<List<PostGetResponseDto>?>> GetAllPosts()
        {
            var resultList = await _service.GetAllPostsAsync();
            return Ok(resultList);
        }

        [HttpGet("{id:long}")]
        [SwaggerOperation(Summary = "Get Post By Id")]
        public async Task<ActionResult<PostGetResponseDto?>> GetPostById([FromRoute] long id)
        {
            var result = await _service.GetPostByIdAsync(id);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpGet("nickname/{nickname}")]
        [SwaggerOperation(Summary = "Get Posts By User Nickname")]
        public async Task<ActionResult<List<PostGetResponseDto>?>> GetPostsByNickname(string nickname)
        {
            var result = await _service.GetPostsByUserNickname(nickname);
            if (result == null || result.Count == 0)
                return NoContent();
            return Ok(result);
        }

        [HttpPatch]
        [Authorize]
        [SwaggerOperation(Summary = "Edit Post")]
        public async Task<ActionResult<PostPatchResponseDto>?> UpdatePostDetail([FromBody] PostPatchRequestDto request)
        {
            try
            {
                var response = await _service.UpdatePostAsync(request);
                if (response == null) return NotFound();
                return Ok(response);
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

        [HttpDelete("{id:long}")]
        [Authorize]
        [SwaggerOperation(Summary = "Delete Post")]
        public async Task<IActionResult> DeletePost([FromRoute] long id)
        {
            try
            {
                await _service.DeletePostAsync(id);
                return NoContent();
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