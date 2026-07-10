using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Tangle.AspNetCore.Config;

namespace Tangle.AspNetCore.Security;

public sealed class InternalAccessAuthorizationFilter(IOptions<InternalAccessOptions> options) : IAuthorizationFilter
{
    public const string HeaderName = "X-Internal-Secret";

    private readonly InternalAccessOptions _options = options.Value;

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (string.IsNullOrWhiteSpace(_options.Secret))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var provided)
            || provided != _options.Secret)
        {
            context.Result = new UnauthorizedResult();
        }
    }
}
