using Api.Domain.Users.Dto;
using Api.Domain.Users.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Api.Domain.Users.Api
{
    [ApiController]
    [Route("api/users")]
    public class UserController : ControllerBase
    {
        private readonly UserService _service;
        public UserController(UserService service)
        {
            _service = service;
        }

        [HttpGet]
        [SwaggerOperation(Summary = "Get All Users")]
        public async Task<ActionResult<List<UserGetResponseDto>>> GetAllUsers()
        {
            var resultList = await _service.GetAllUsersAsync();
            return Ok(resultList);
        }

        [HttpGet("{id:long}")]
        [SwaggerOperation(Summary = "Get User By Id")]
        public async Task<ActionResult<UserGetResponseDto?>> GetUserById([FromRoute] long id)
        {
            var response = await _service.GetUserByIdAsync(id);
            if (response == null) return NotFound();
            return Ok(response);
        }

        [HttpPatch]
        [Authorize]
        [SwaggerOperation(Summary = "Update User Detail")]
        public async Task<ActionResult<UserPatchResponseDto>> UpdateUserDetail([FromBody] UserPatchRequestDto request)
        {
            var response = await _service.UpdateUserDetailAsync(request);
            return Ok(response);
        }

        [HttpPatch("privacy")]
        [Authorize]
        [SwaggerOperation(Summary = "Update privacy settings for the logged-in user")]
        public async Task<ActionResult<UserPrivacySettingsResponseDto>> UpdatePrivacySettings(
            [FromBody] UserPrivacySettingsUpdateRequestDto request)
        {
            var response = await _service.UpdatePrivacySettingsAsync(request);
            return Ok(response);
        }

        [HttpDelete("{id:long}")]
        [Authorize]
        [SwaggerOperation(Summary = "Delete User")]
        public async Task<IActionResult> DeleteUser([FromRoute] long id)
        {
            await _service.DeleteUserAsync(id);
            return NoContent();
        }
    }
}
