using Microsoft.AspNetCore.SignalR;

namespace Location.Security;

public sealed class SubClaimUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User?.FindFirst("sub")?.Value;
}
