using LanguageExt;
using Microsoft.AspNetCore.Mvc;

namespace TeslaSolarCharger.SharedBackend.Extensions;

public static class FinExtensions
{
    public static IActionResult ToOk<TResult>(this Fin<TResult> result)
    {
        return result.Match<IActionResult>(
            Succ: succ => new OkObjectResult(succ),
            Fail: err =>
            {
                if (!err.IsExceptional)
                {
                    return new ObjectResult(new ProblemDetails() { Detail = err.Message, Status = 500, });
                }

                var exception = err.ToException();
                if (exception is HttpRequestException httpRequestException)
                {
                    var problemDetails = new ProblemDetails()
                    {
                        Detail = $"Error while calling API from backend: {httpRequestException.Message}",
                        Status = (int?)httpRequestException.StatusCode,
                    };
                    return new ObjectResult(problemDetails)
                    {
                        StatusCode = problemDetails.Status,
                    };
                }
                return new ObjectResult(new ProblemDetails()
                {
                    Detail = err.Message,
                    Status = 500,
                });
            });
    }
}
