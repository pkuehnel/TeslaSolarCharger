using LanguageExt;
using LanguageExt.Common;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Net;
using System.Net.Http;
using TeslaSolarCharger.SharedBackend.Extensions;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.SharedBackend;

/// <summary>
/// <see cref="FinExtensions.ToOk{TResult}"/> relies on how LanguageExt tells expected from exceptional errors, which
/// changed between the v5 betas, so these tests pin the HTTP results the controllers hand to the client.
/// </summary>
public class FinExtensionsTests(ITestOutputHelper outputHelper) : TestBase(outputHelper)
{
    [Fact]
    public void ToOk_Success_ReturnsOkWithValue()
    {
        var value = new object();

        var actionResult = Fin.Succ(value).ToOk();

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Same(value, okResult.Value);
    }

    [Fact]
    public void ToOk_ExpectedError_ReturnsInternalServerErrorWithMessage()
    {
        var actionResult = Fin.Fail<object>("No Backend token found.").ToOk();

        var problemDetails = AssertProblemDetails(actionResult, 500);
        Assert.Equal("No Backend token found.", problemDetails.Detail);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public void ToOk_HttpRequestExceptionWithStatusCode_ForwardsStatusCode(HttpStatusCode statusCode)
    {
        var exception = new HttpRequestException("Backend unreachable", null, statusCode);

        var actionResult = Fin.Fail<object>(Error.New(exception)).ToOk();

        var problemDetails = AssertProblemDetails(actionResult, (int)statusCode);
        Assert.Equal((int)statusCode, Assert.IsType<ObjectResult>(actionResult).StatusCode);
        Assert.Equal("Error while calling API from backend: Backend unreachable", problemDetails.Detail);
    }

    [Fact]
    public void ToOk_HttpRequestExceptionWithoutStatusCode_HasNoStatusCode()
    {
        var exception = new HttpRequestException("Backend unreachable");

        var actionResult = Fin.Fail<object>(Error.New(exception)).ToOk();

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Null(objectResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Null(problemDetails.Status);
        Assert.Equal("Error while calling API from backend: Backend unreachable", problemDetails.Detail);
    }

    [Fact]
    public void ToOk_OtherException_ReturnsInternalServerErrorWithExceptionMessage()
    {
        var exception = new InvalidOperationException("Something broke");

        var actionResult = Fin.Fail<object>(Error.New(exception)).ToOk();

        var problemDetails = AssertProblemDetails(actionResult, 500);
        Assert.Equal("Something broke", problemDetails.Detail);
    }

    /// <summary>ASP.NET Core takes the response status code from <see cref="ProblemDetails.Status"/> when the result does not set one.</summary>
    private static ProblemDetails AssertProblemDetails(IActionResult actionResult, int expectedStatus)
    {
        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        var problemDetails = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(expectedStatus, problemDetails.Status);
        return problemDetails;
    }
}
