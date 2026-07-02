using Api.Global.Config;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace Api.Global.Security;

public sealed class WorkerCallbackAuthorizationFilter(IOptions<WorkerCallbackOptions> workerCallbackOptions) : IAuthorizationFilter
{
    public const string HeaderName = "X-Worker-Callback-Secret";

    private readonly WorkerCallbackOptions _options = workerCallbackOptions.Value;

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
