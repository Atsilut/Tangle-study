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
    }
}