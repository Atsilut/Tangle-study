using Api.Domain.Comments.Dto;
using Api.Domain.Comments.Service;
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
    }
}
