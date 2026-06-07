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
    public class UserBlockController(UserBlockService service) : ControllerBase
    {
        private readonly UserBlockService _service = service;

        [HttpPost]
        [SwaggerOperation(Summary = "Block a user (shared across community features)")]
        public async Task<IActionResult> BlockUser([FromBody] UserBlockCreateRequestDto request)
        {
            await _service.BlockUserAsync(request);
            return Ok();
        }

        [HttpGet("me")]
        [SwaggerOperation(Summary = "List users I have blocked")]
        public async Task<ActionResult<List<UserBlockGetResponseDto>?>> GetMyBlocks()
        {
            var response = await _service.GetMyBlocksAsync();
            if (response == null) return NoContent();
            return Ok(response);
        }

        [HttpDelete("{id:long}")]
        [SwaggerOperation(Summary = "Unblock a user")]
        public async Task<IActionResult> DeleteBlock([FromRoute] long id)
        {
            await _service.DeleteBlockByIdAsync(id);
            return NoContent();
        }
    }
}
