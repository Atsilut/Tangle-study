using Api.Domain.Users.Dto;
using Api.Domain.Users.Service;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Api.Domain.Users.Api
{
    [ApiController]
    [Route("api")]
    public class LoginController(LoginService service) : ControllerBase
    {
        private readonly LoginService _service = service;

        [HttpPost("join")]
        [SwaggerOperation(Summary = "Sign Up")]
        public async Task<IActionResult> CreateUser([FromBody] UserCreateRequestDto request)
        {
            await _service.CreateUserAsync(request);
            return Created();
        }

        [HttpPost("login")]
        [SwaggerOperation(Summary = "Login")]
        public async Task<ActionResult<LoginResponseDto?>> Login([FromBody] LoginRequestDto request)
        {
            if (request == null || string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
                return BadRequest("Email and password are required.");
            var response = await _service.LoginUserAsync(request);
            if (response == null) return Unauthorized();
            return Ok(response);
        }
    }
}
