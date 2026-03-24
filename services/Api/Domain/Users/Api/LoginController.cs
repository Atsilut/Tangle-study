using Api.Domain.Users.Dto;
using Api.Domain.Users.Service;
using Api.Global.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Api.Domain.Users.Api
{
    [ApiController]
    [Route("api")]
    public class LoginController : ControllerBase
    {
        private readonly LoginService _service;
        public LoginController(LoginService service)
        {
            _service = service;
        }

        [HttpPost("/join")]
        [SwaggerOperation(Summary = "Sign Up")]
        public async Task<IActionResult> CreateUser([FromBody] Dto.UserCreateRequestDto request)
        {
            try
            {
                await _service.CreateUserAsync(request);
                return NoContent();
            }
            catch (EntityAlreadyExistsException ex)
            {
                return BadRequest();
            }
        }

        [HttpPost("/login")]
        [SwaggerOperation(Summary = "Login")]
        public async Task<ActionResult<LoginResponseDto?>> Login([FromBody] LoginRequestDto request)
        {
            if (request == null || string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest("Email and password are required.");
            }
            var response = await _service.LoginUserAsync(request);
            if (response == null) return Unauthorized();
            return Ok(response);
        }
    }
}
