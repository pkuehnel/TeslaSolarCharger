using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Dtos;

namespace TeslaSolarCharger.Server.Helper;

public static class ResultExtensions
{
    /// <summary>
    /// Returns the result's data, or its problem details with their status code when it failed. A failure without
    /// problem details becomes an internal server error that carries the error message.
    /// </summary>
    public static IActionResult ToOk<T>(this Result<T> result)
    {
        if (!result.HasError)
        {
            return new OkObjectResult(result.Data);
        }

        var problemDetails = result.ProblemDetails ?? new ProblemDetails
        {
            Detail = result.ErrorMessage,
            Status = StatusCodes.Status500InternalServerError,
        };
        return new ObjectResult(problemDetails)
        {
            StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError,
        };
    }
}
