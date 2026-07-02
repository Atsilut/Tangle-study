using Media.Global.Exceptions;
using Microsoft.AspNetCore.Http;

namespace Media.Tests.Global;

public sealed class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_Returns403_ForAccessForbiddenException()
    {
        var handler = new GlobalExceptionHandler();
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var exception = new AccessForbiddenException("Forbidden");

        var handled = await handler.TryHandleAsync(context, exception, TestContext.Current.CancellationToken);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }
}
