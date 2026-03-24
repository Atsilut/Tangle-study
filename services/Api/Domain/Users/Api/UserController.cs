using Api.Domain.Users.Dto;
using Api.Domain.Users.Service;
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

        [HttpPost]
        [SwaggerOperation(Summary = "Sign Up")]
        public async Task<IActionResult> CreateUser([FromBody] Dto.UserCreateRequestDto request)
        {
            await _service.CreateUserAsync(request);
            return Ok();
        }

        [HttpGet]
        [SwaggerOperation(Summary = "Get All Users")]
        public async Task<IActionResult> GetAllUsers()
        {
            var resultUsers = await _service.GetAllUsersAsync();
            return Ok(resultUsers);
        }

        [HttpGet]
        [SwaggerOperation(Summary = "Get User By Id")]
        public async Task<ActionResult<UserGetResponseDto?>> GetUserById([FromQuery] Guid id)
        {
            var response = await _service.GetUserByIdAsync(id);
            if (response == null) return NotFound();
            return Ok(response);
        }
    }
}