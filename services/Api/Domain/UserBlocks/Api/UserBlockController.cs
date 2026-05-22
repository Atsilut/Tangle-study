using Api.Domain.UserBlocks.Dto;
using Api.Domain.UserBlocks.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Api.Domain.UserBlocks.Api
{
    [ApiController]
    [Route("api/users/blocks")]
    [Authorize]
    public class UserBlockController : ControllerBase
    {
        private readonly UserBlockService _service;

        public UserBlockController(UserBlockService service)
        {
            _service = service;
        }

        [HttpPost]
        [SwaggerOperation(Summary = "Block a user (shared across community features)")]
        public async Task<IActionResult> BlockUser([FromBody] UserBlockCreateRequestDto request)
        {
            await _service.BlockUserAsync(request);
            return Ok();
        }
    }
}
