using Media.Global.Config;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace Media.Global.Security;

public sealed class InternalServiceAuthorizationFilter(IOptions<MediaOptions> mediaOptions) : IAuthorizationFilter
{
    public const string HeaderName = "X-Internal-Secret";

    private readonly MediaOptions _mediaOptions = mediaOptions.Value;

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (string.IsNullOrWhiteSpace(_mediaOptions.InternalServiceSecret))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var provided)
            || provided != _mediaOptions.InternalServiceSecret)
        {
            context.Result = new UnauthorizedResult();
        }
    }
}
