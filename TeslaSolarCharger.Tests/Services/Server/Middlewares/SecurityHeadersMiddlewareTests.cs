using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Moq;
using TeslaSolarCharger.Server.Middlewares;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server.Middlewares
{
    public class SecurityHeadersMiddlewareTests
    {
        [Fact]
        public async Task InvokeAsync_AddsSecurityHeaders()
        {
            // Arrange
            var context = new DefaultHttpContext();
            var next = new Mock<RequestDelegate>();
            var middleware = new SecurityHeadersMiddleware(next.Object);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.True(context.Response.Headers.ContainsKey("X-Content-Type-Options"));
            Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"]);

            Assert.True(context.Response.Headers.ContainsKey("Referrer-Policy"));
            Assert.Equal("strict-origin-when-cross-origin", context.Response.Headers["Referrer-Policy"]);

            next.Verify(n => n(context), Times.Once);
        }
    }
}
