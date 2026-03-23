using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Swashbuckle.AspNetCore.Annotations;

namespace Api.Domain.Users.Dto
{
    public class CreateUserDto
    {
        [Required]
        [RegularExpression(
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            ErrorMessage = "Email format is invalid."
        )]
        [SwaggerSchema(Description = "User email address", Format = "email")]
        [DefaultValue("tangler@gmail.com")]
        public string Email { get; set; }

        [Required]
        [MinLength(8)]
        [RegularExpression(
            @"^(?=.*[A-Za-z])(?=.*\d)[A-Za-z\d!@#$%^&*()_+=-]{8,32}$",
            ErrorMessage = "Password must be 8-32 characters and include letters and numbers."
        )]
        [SwaggerSchema(Description = "Password (8-32 characters, including letters and numbers)")]
        [DefaultValue("password123!")]
        public string Password { get; set; }

        [Required]
        [SwaggerSchema(Description = "User nickname")]
        [DefaultValue("TangleTangle")]
        public string Nickname { get; set; }
    }
}
