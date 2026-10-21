using CabinBooking.Application.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CabinBooking.Api.Errors;

/// <summary>Turns application exceptions into RFC 7807 ProblemDetails responses.</summary>
public class AppExceptionHandler(IProblemDetailsService problemDetails, ILogger<AppExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            AppValidationException => (StatusCodes.Status400BadRequest, "Invalid request"),
            NotFoundException => (StatusCodes.Status404NotFound, "Not found"),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden"),
            ConflictException => (StatusCodes.Status409Conflict, "Booking conflict"),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Booking was changed by someone else"),
            TimeoutException => (StatusCodes.Status503ServiceUnavailable, "Busy, please retry"),
            _ => (StatusCodes.Status500InternalServerError, "Something went wrong")
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception");

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = exception switch
                {
                    AppException => exception.Message,
                    DbUpdateConcurrencyException => "Please reload and try again.",
                    TimeoutException => exception.Message,
                    _ when status == StatusCodes.Status500InternalServerError =>
                        $"[DEBUG] {exception.GetType().Name}: {exception.Message}",
                    _ => null
                }
            }
        });
    }
}
