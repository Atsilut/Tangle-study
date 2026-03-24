using Api.Domain.Users.Dto;
using Api.Domain.Users.Service;
using Api.Global.Exceptions;
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
        public async Task<ActionResult<List<UserGetResponseDto>?>> GetAllUsers()
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
        [SwaggerOperation(Summary = "Update User Detail")]
        public async Task<ActionResult<UserPatchResponseDto>?> UpdateUserDetail([FromBody] UserPatchRequestDto request)
        {
            try
            {
                var response = await _service.UpdateUserDetailAsync(request);
                if (response == null) return NotFound();
                return Ok(response);
            }
            catch (EntityNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpDelete]
        [SwaggerOperation(Summary = "Delete User")]
        public async Task<IActionResult> DeleteUser([FromRoute] long id)
        {
            try
            {
                await _service.DeleteUserAsync(id);
                return NoContent();
            }
            catch (EntityNotFoundException)
            {
                return NotFound();
            }
        }
    }
}