using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Net;
using TeslaSolarCharger.Server.Exceptions;

namespace TeslaSolarCharger.Server.Middlewares;

public class ApiExceptionFilterAttribute : ExceptionFilterAttribute
{
    private readonly ILogger<ApiExceptionFilterAttribute> _logger;
    private readonly IDictionary<Type, Action<ExceptionContext>> _exceptionHandlers;

    public ApiExceptionFilterAttribute(ILogger<ApiExceptionFilterAttribute> logger)
    {
        _logger = logger;
        _exceptionHandlers = new Dictionary<Type, Action<ExceptionContext>>
        {
            { typeof(ValidationException), HandleValidationException },
            { typeof(UnauthorizedAccessException), HandleUnauthorizedExcepction },
            { typeof(FileNotFoundException), HandleFileNotFoundException },
            { typeof(ArgumentException), HandleArgumentException },
            { typeof(ProtocolViolationException), HandleProtocolViolationException },
            { typeof(InvalidOperationException), HandleInvalidOperationException },
        };
    }

    public override void OnException(ExceptionContext context)
    {
        HandleException(context);
        base.OnException(context);
    }

    private void HandleException(ExceptionContext context)
    {
        _logger.LogError(context.Exception, context.Exception.Message);
        if (_exceptionHandlers.TryGetValue(context.Exception.GetType(), out var handler))
        {
            handler(context);
            return;
        }
        HandleUnknwonException(context);
    }

    private void HandleValidationException(ExceptionContext context)
    {
        var exception = context.Exception as ValidationException;
        var problemDetails = new ValidationProblemDetails(exception!.Errors)
        {
            Title = "One or more validation failures have occurred.",
            Status = StatusCodes.Status422UnprocessableEntity,
            Detail = exception!.Message,
            Instance = context.HttpContext.Request.Path.Value,
        };
        var result = new ObjectResult(problemDetails);
        context.Result = result;
        context.ExceptionHandled = true;
    }

    private void HandleUnauthorizedExcepction(ExceptionContext context)
    {
        var exception = context.Exception as UnauthorizedAccessException;
        var problemDetails = new ProblemDetails
        {
            Title = "Unauthorized",
            Status = StatusCodes.Status401Unauthorized,
            Detail = exception?.Message,
            Instance = context.HttpContext.Request.Path.Value,
        };
        var result = new ObjectResult(problemDetails);
        context.Result = result;
        context.ExceptionHandled = true;
    }

    private void HandleFileNotFoundException(ExceptionContext context)
    {
        var exception = context.Exception as UnauthorizedAccessException;
        var problemDetails = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
            Title = "File not found",
            Status = StatusCodes.Status404NotFound,
            Detail = exception?.Message,
            Instance = context.HttpContext.Request.Path.Value,
        };
        var result = new ObjectResult(problemDetails);
        context.Result = result;
        context.ExceptionHandled = true;
    }

    private void HandleArgumentException(ExceptionContext context)
    {
        var exception = context.Exception as ArgumentException;
        var problemDetails = new ProblemDetails
        {
            Title = "Bad request",
            Status = StatusCodes.Status400BadRequest,
            Detail = exception?.Message,
            Instance = context.HttpContext.Request.Path.Value,
        };
        var result = new ObjectResult(problemDetails);
        context.Result = result;
        context.ExceptionHandled = true;
    }

    private void HandleProtocolViolationException(ExceptionContext context)
    {
        var exception = context.Exception as ProtocolViolationException;
        var problemDetails = new ProblemDetails
        {
            Title = "Bad request",
            Status = StatusCodes.Status400BadRequest,
            Detail = exception?.Message,
            Instance = context.HttpContext.Request.Path.Value,
        };
        var result = new ObjectResult(problemDetails);
        context.Result = result;
        context.ExceptionHandled = true;
    }

    private void HandleInvalidOperationException(ExceptionContext context)
    {
        var exception = context.Exception as InvalidOperationException;
        var problemDetails = new ProblemDetails
        {
            Title = "Precondition failed",
            Status = StatusCodes.Status412PreconditionFailed,
            Detail = exception?.Message,
            Instance = context.HttpContext.Request.Path.Value,
        };
        var result = new ObjectResult(problemDetails);
        context.Result = result;
        context.ExceptionHandled = true;
    }

    private void HandleUnknwonException(ExceptionContext context)
    {
        var problemDetails = new ProblemDetails
        {
            Title = "An unexpected error occurred!",
            Status = StatusCodes.Status500InternalServerError,
            Detail = context.Exception.Message,
            Instance = context.HttpContext.Request.Path.Value,
        };
        var result = new ObjectResult(problemDetails);
        context.Result = result;
        context.ExceptionHandled = true;
    }
}
