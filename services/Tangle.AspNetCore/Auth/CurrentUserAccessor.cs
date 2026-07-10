using System.Security.Claims;

namespace Tangle.AspNetCore.Auth;

public sealed class CurrentUserAccessor(IHttpContextAccessor httpContextAccessor)
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    public long GetUserIdFromLogin() => long.Parse(
        _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
        ?? throw new UnauthorizedAccessException("Unauthorized access"));

    public long? TryGetUserIdFromLogin()
    {
        var sub = _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value;
        return long.TryParse(sub, out var id) ? id : null;
    }

    public long? TryGetViewerUserId() => TryGetUserIdFromLogin();

    public long GetUserIdFromPrincipal(ClaimsPrincipal? principal) => long.Parse(
        principal?.FindFirst("sub")?.Value
        ?? throw new UnauthorizedAccessException("Unauthorized access"));
}
