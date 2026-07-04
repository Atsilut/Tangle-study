using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Social.Friendships.Dto;

public record FriendRequestCreateRequestDto
{
    [Required]
    [SwaggerSchema(Description = "Target user id to send a friend request to")]
    [DefaultValue(2)]
    public required long AddresseeId { get; init; }
}
