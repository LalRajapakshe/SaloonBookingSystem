using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using SalonBooking.Application.Common;

namespace SalonBooking.API.Infrastructure;

public sealed class ApiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title) = exception switch
        {
            UnauthorizedAccessException when
                httpContext.User.Identity?.IsAuthenticated == true
                => (StatusCodes.Status403Forbidden, "Forbidden"),
            UnauthorizedAccessException
                => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            KeyNotFoundException
                => (StatusCodes.Status404NotFound, "Not Found"),
            SchedulingConflictException conflict
                => (StatusCodes.Status409Conflict, conflict.Message),
            DbUpdateConcurrencyException
                => (StatusCodes.Status409Conflict, "The record was changed by someone else. Reload it and try again."),
            SchedulingException scheduling
                => (StatusCodes.Status400BadRequest, scheduling.Message),
            _ => (0, string.Empty)
        };

        if (statusCode == 0)
        {
            return false;
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(
            new { title },
            cancellationToken);

        return true;
    }
}
