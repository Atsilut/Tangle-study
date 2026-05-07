using Api.Domain.Posts.Dto;
using Api.Domain.Posts.Service;
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

        [HttpGet]
        [SwaggerOperation(Summary = "Get Post By Id")]
        public async Task<ActionResult<PostGetResponseDto?>> GetPostById([FromQuery] long id)
        {
            var result = await _service.GetPostByIdAsync(id);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpGet]
        [SwaggerOperation(Summary = "Get Posts By User Nickname")]
        public async Task<ActionResult<List<PostGetResponseDto>?>> GetPostsByNickname(string nickname)
        {
            var result = await _service.GetPostsByUserNickname(nickname);
            return Ok(result);
        }
    }
}