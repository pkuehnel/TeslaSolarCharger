using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Net;
using TeslaSolarCharger.Server.Exceptions;

namespace TeslaSolarCharger.Server.Middlewares;

public class ErrorHandlingMiddleware(ILogger<ErrorHandlingMiddleware> logger) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            //As Unauthorized dos not necessaryl throw an exception, we need to check the status code
            await next(context).ConfigureAwait(false);
            if (context.Response.StatusCode == StatusCodes.Status401Unauthorized)
            {
                await HandleUnauthorizedAsync(context).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex).ConfigureAwait(false);
        }
    }

    private Task HandleUnauthorizedAsync(HttpContext context)
    {
        var authError = context.Items["AuthError"] as string;
        var detail = authError ?? "Authentication is required or the token is invalid.";

        logger.LogWarning("Unauthorized access attempt detected: {Path}; AuthError: {authError}", context.Request.Path, authError);


        var problemDetails = new ProblemDetails
        {
            Title = "Unauthorized",
            Status = StatusCodes.Status401Unauthorized,
            Detail = detail,
            Instance = context.Request.Path.Value,
        };

        context.Response.ContentType = "application/json";
        var result = JsonConvert.SerializeObject(problemDetails);
        return context.Response.WriteAsync(result);
    }

    private Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var problemDetails = new ProblemDetails
        {
            Title = "An unexpected error occurred!",
            Status = StatusCodes.Status500InternalServerError,
            Detail = exception.Message,
            Instance = context.Request.Path.Value,
        };

        switch (exception)
        {
            case ValidationException validationException:
                problemDetails.Title = "Validation failed";
                problemDetails.Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1";
                problemDetails.Status = StatusCodes.Status422UnprocessableEntity;
                problemDetails.Detail = "One or more validation failures have occurred.";
                problemDetails.Extensions.Add("validationErrors", validationException.Errors);
                break;

            case UnauthorizedAccessException e:
                logger.LogWarning(e, e.Message);
                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                break;

            case FileNotFoundException e:
                logger.LogError(e, e.Message);
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                break;
            case ArgumentException e:
                logger.LogError(e, e.Message);
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                break;

            case ProtocolViolationException e:
                logger.LogError(e, e.Message);
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                break;

            case InvalidOperationException e:
                logger.LogError(e, e.Message);
                context.Response.StatusCode = (int)HttpStatusCode.PreconditionFailed;
                break;

            case { } e:
                logger.LogError(e, e.Message);
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                break;
        }

        context.Response.ContentType = "application/json";
        var result = JsonConvert.SerializeObject(problemDetails);
        return context.Response.WriteAsync(result);
    }
}
