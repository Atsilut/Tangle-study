using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Users.Dto
{
    public record LoginRequestDto
    {
        [Required]
        [StringLength(100, MinimumLength = 1)]
        [RegularExpression(
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            ErrorMessage = "Email format is invalid."
        )]
        [SwaggerSchema(Description = "User email address", Format = "email")]
        [DefaultValue("tangler@gmail.com")]
        public required string Email { get; init; }

        [Required]
        [StringLength(100, MinimumLength = 1)]
        [MinLength(8)]
        [RegularExpression(
            @"^(?=.*[A-Za-z])(?=.*\d)[A-Za-z\d!@#$%^&*()_+=-]{8,32}$",
            ErrorMessage = "Password must be 8-32 characters and include letters and numbers."
        )]
        [SwaggerSchema(Description = "Password (8-32 characters, including letters and numbers)")]
        [DefaultValue("password123!")]
        public required string Password { get; init; }
    }

    public record LoginResponseDto(string AccessToken);
}
