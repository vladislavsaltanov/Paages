using Microsoft.AspNetCore.Diagnostics;
using Paages.Domain.Exceptions;

namespace Paages.Api.ExceptionHandling;

public class AuthExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        int? statusCode = exception switch
        {
            EmailAlreadyRegisteredException => StatusCodes.Status409Conflict,
            InvalidCredentialsException => StatusCodes.Status401Unauthorized,
            InvalidRefreshTokenException => StatusCodes.Status401Unauthorized,
            _ => null
        };

        if (statusCode is null)
            return false;

        httpContext.Response.StatusCode = statusCode.Value;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = { Title = exception.Message, Status = statusCode }
        });
    }
}