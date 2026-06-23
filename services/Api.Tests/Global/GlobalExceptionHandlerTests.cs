using Api.Global.Exceptions;
using Microsoft.AspNetCore.Http;

namespace Api.Tests.Global;

public sealed class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_Returns401_ForUnauthorizedAccessException()
    {
        // Arrange
        var handler = new GlobalExceptionHandler();
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var exception = new UnauthorizedAccessException("Unauthorized access");

        // Act
        var handled = await handler.TryHandleAsync(context, exception, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(handled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task TryHandleAsync_Returns404_ForEntityNotFoundException()
    {
        // Arrange
        var handler = new GlobalExceptionHandler();
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var exception = new EntityNotFoundException("Block not found");

        // Act
        var handled = await handler.TryHandleAsync(context, exception, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(handled);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    [Fact]
    public async Task TryHandleAsync_Returns400_ForArgumentException()
    {
        // Arrange
        var handler = new GlobalExceptionHandler();
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var exception = new ArgumentException("Cannot block yourself.");

        // Act
        var handled = await handler.TryHandleAsync(context, exception, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task TryHandleAsync_Returns400_ForEntityNotFoundExceptionWith400Status()
    {
        // Arrange
        var handler = new GlobalExceptionHandler();
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var exception = new EntityNotFoundException("User not found", StatusCodes.Status400BadRequest);

        // Act
        var handled = await handler.TryHandleAsync(context, exception, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task TryHandleAsync_Returns409_ForEntityAlreadyExistsException()
    {
        // Arrange
        var handler = new GlobalExceptionHandler();
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var exception = new EntityAlreadyExistsException("A user with nickname 'taken' already exists.");

        // Act
        var handled = await handler.TryHandleAsync(context, exception, TestContext.Current.CancellationToken);

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
        var handled = await handler.TryHandleAsync(context, exception, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(handled);
    }
}
