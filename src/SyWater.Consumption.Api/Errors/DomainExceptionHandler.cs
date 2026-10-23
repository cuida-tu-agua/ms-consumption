using Microsoft.AspNetCore.Diagnostics;
using SyWater.Consumption.Application.Ports.Out;
using SyWater.Consumption.Domain.Common;
using SyWater.Consumption.Domain.Consumption;
using SyWater.Consumption.Domain.Goals;

namespace SyWater.Consumption.Api.Errors;

public sealed class DomainExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, code) = exception switch
        {
            PlaceNotFoundException e => (StatusCodes.Status404NotFound, e.Code),
            InvalidPeriodException e => (StatusCodes.Status400BadRequest, e.Code),
            UnknownTimeZoneException e => (StatusCodes.Status400BadRequest, e.Code),
            InvalidGoalTargetException e => (StatusCodes.Status400BadRequest, e.Code),
            ExternalServiceUnavailableException => (StatusCodes.Status503ServiceUnavailable, "service.unavailable"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "auth.invalid_token"),
            _ => (0, ""),
        };

        if (status == 0) return false; // unknown error: ASP.NET Core answers a generic 500

        context.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails =
            {
                Status = status,
                Title = code,
                Detail = exception.Message,
                Type = exception is DomainException ? $"https://sywater.dev/errors/{code}" : null,
            },
        });
    }
}
