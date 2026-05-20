using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Swashbuckle.AspNetCore.Annotations;

namespace Api.Domain.Users.Dto
{
    public record UserCreateRequestDto
    {
        [Required]
        [StringLength(100, MinimumLength = 1)]
        [RegularExpression(
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            ErrorMessage = "Email format is invalid."
        )]
        [SwaggerSchema(Description = "User email address", Format = "email")]
        [DefaultValue("tangler@gmail.com")]
        public string Email { get; init; }

        [Required]
        [StringLength(100, MinimumLength = 1)]
        [MinLength(8)]
        [RegularExpression(
            @"^(?=.*[A-Za-z])(?=.*\d)[A-Za-z\d!@#$%^&*()_+=-]{8,32}$",
            ErrorMessage = "Password must be 8-32 characters and include letters and numbers."
        )]
        [SwaggerSchema(Description = "Password (8-32 characters, including letters and numbers)")]
        [DefaultValue("password123!")]
        public string Password { get; init; }

        [Required]
        [StringLength(100, MinimumLength = 1)]
        [SwaggerSchema(Description = "User nickname")]
        [DefaultValue("TangleTangle")]
        public string Nickname { get; init; }
    }

    public record UserPatchRequestDto
    {
        public UserPatchRequestDto(long id, string nickname)
        {
            Id = id;
            Nickname = nickname;
        }
        [Required]
        public long Id { get; init; }
        [Required]
        [StringLength(100, MinimumLength = 1)]
        [SwaggerSchema(Description = "User nickname")]
        [DefaultValue("EditedTangle")]
        public string Nickname { get; init; }
    }
}
