using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Net;
using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Helper;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

public class ResultExtensionsTests(ITestOutputHelper outputHelper) : TestBase(outputHelper)
{
    [Fact]
    public void ToOk_Success_ReturnsOkWithData()
    {
        var data = new List<string> { "value", };

        var actionResult = new Result<List<string>>(data, null, null).ToOk();

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Same(data, okResult.Value);
    }

    [Fact]
    public void ToOk_SuccessWithoutData_ReturnsOkWithoutValue()
    {
        var actionResult = new Result<object>(null, null, null).ToOk();

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Null(okResult.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ToOk_ProblemDetailsWithoutErrorMessage_IsNoError(string? errorMessage)
    {
        var actionResult = new Result<string>("data", errorMessage, new ProblemDetails { Status = 400, }).ToOk();

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal("data", okResult.Value);
    }

    [Fact]
    public void ToOk_ErrorWithoutProblemDetails_ReturnsInternalServerErrorWithMessage()
    {
        var actionResult = new Result<string>(null, "No Backend token found.", null).ToOk();

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(500, objectResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(500, problemDetails.Status);
        Assert.Equal("No Backend token found.", problemDetails.Detail);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public void ToOk_ErrorWithProblemDetails_ReturnsProblemDetailsWithTheirStatusCode(HttpStatusCode statusCode)
    {
        var problemDetails = new ProblemDetails { Detail = "Backend unreachable", Status = (int)statusCode, };

        var actionResult = new Result<string>(null, "Error message", problemDetails).ToOk();

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal((int)statusCode, objectResult.StatusCode);
        Assert.Same(problemDetails, objectResult.Value);
    }

    [Fact]
    public void ToOk_ErrorWithProblemDetailsWithoutStatus_ReturnsInternalServerError()
    {
        var problemDetails = new ProblemDetails { Detail = "Backend unreachable", };

        var actionResult = new Result<string>(null, "Error message", problemDetails).ToOk();

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(500, objectResult.StatusCode);
        Assert.Same(problemDetails, objectResult.Value);
    }
}
