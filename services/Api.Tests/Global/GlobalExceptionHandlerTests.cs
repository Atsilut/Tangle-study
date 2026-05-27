using Api.Global.Exceptions;
using Microsoft.AspNetCore.Http;

namespace Api.Tests.Global;

public sealed class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_Returns409_ForEntityAlreadyExistsException()
    {
        // Arrange
        var handler = new GlobalExceptionHandler();
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var exception = new EntityAlreadyExistsException("A user with nickname 'taken' already exists.");

        // Act
        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        Assert.True(handled);
        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
    }

    [Fact]
    public async Task TryHandleAsync_ReturnsFalse_ForUnhandledException()
    {
        // Arrange
        var handler = new GlobalExceptionHandler();
        var context = new DefaultHttpContext();
        var exception = new InvalidOperationException("unexpected");

        // Act
        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        Assert.False(handled);
    }
}
