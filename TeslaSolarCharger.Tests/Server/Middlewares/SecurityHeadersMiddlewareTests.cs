using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Moq;
using TeslaSolarCharger.Server.Middlewares;
using Xunit;

namespace TeslaSolarCharger.Tests.Server.Middlewares;

public class SecurityHeadersMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_AddsSecurityHeaders()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var next = new Mock<RequestDelegate>();
        next.Setup(n => n(context)).Returns(Task.CompletedTask);

        var middleware = new SecurityHeadersMiddleware(next.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(context.Response.Headers.ContainsKey("X-Content-Type-Options"));
        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"]);

        Assert.True(context.Response.Headers.ContainsKey("Referrer-Policy"));
        Assert.Equal("strict-origin-when-cross-origin", context.Response.Headers["Referrer-Policy"]);
    }
}
