using Api.Domain.Users.Service;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Api.Domain.Users.Api
{
    [ApiController]
    [Route("api")]
    public class LoginController : ControllerBase
    {
        private readonly UserService _service;

        [HttpPost("/join")]
        [SwaggerOperation(Summary = "Sign Up")]
        public async Task<IActionResult> CreateUser([FromBody] Dto.UserCreateRequestDto request)
        {
            await _service.CreateUserAsync(request);
            return Ok();
        }
    }
}
