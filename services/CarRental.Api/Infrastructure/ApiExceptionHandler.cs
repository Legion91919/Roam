using CarRental.Application.Common.Exceptions;
using CarRental.Domain.Common;
using CarRental.Domain.Payments;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CarRental.Api.Infrastructure;

/// <summary>
/// Turns exceptions into consistent HTTP answers in ONE place, so controllers contain no try/catch.
/// </summary>
/// <remarks>
///   ValidationException          -> 400 Bad Request          (request shape is wrong)
///   UnauthenticatedException     -> 401 Unauthorized
///   NotFoundException            -> 404 Not Found
///   CapacityExceededException    -> 409 Conflict             (sold out)
///   ConcurrencyConflictException -> 409 Conflict             (lost a race, retries exhausted)
///   PaymentDeclinedException     -> 402 Payment Required     (card declined; "reason" says why)
///   DomainException              -> 422 Unprocessable Entity (business rule broken)
///   anything else                -> 500 (default handler, no internals leaked)
/// Every body carries a stable "code" (e.g. "eligibility.under_age") for the client to switch on.
/// SOLID (O): a new exception type means adding a mapping here, not touching any controller.
/// </remarks>
internal sealed class ApiExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            ValidationException validation => Problem(StatusCodes.Status400BadRequest, "Validation failed", "validation.failed", validation.Message,
                validation.Errors.GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())),
            UnauthenticatedException => Problem(StatusCodes.Status401Unauthorized, "Sign in required", "auth.required", "You need to sign in to do that."),
            NotFoundException notFound => Problem(StatusCodes.Status404NotFound, "Not found", "resource.not_found", notFound.Message),
            CapacityExceededException capacity => Problem(StatusCodes.Status409Conflict, "Not available", capacity.Code, capacity.Message),
            ConcurrencyConflictException conflict => Problem(StatusCodes.Status409Conflict, "Conflict", "concurrency.conflict", conflict.Message),
            PaymentDeclinedException declined => Problem(StatusCodes.Status402PaymentRequired, "Payment declined", declined.Code, declined.Message,
                extensions: new Dictionary<string, object?> { ["reason"] = declined.Reason }),
            DomainException domain => Problem(StatusCodes.Status422UnprocessableEntity, "Business rule violated", domain.Code, domain.Message),
            _ => null,
        };

        if (problem is null)
            return false;   // let the default handler produce a generic 500

        logger.LogInformation("Request rejected with {Status} {Code}", problem.Status, problem.Extensions["code"]);
        httpContext.Response.StatusCode = problem.Status!.Value;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static ProblemDetails Problem(int status, string title, string code, string detail,
        IDictionary<string, string[]>? errors = null, IDictionary<string, object?>? extensions = null)
    {
        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail };
        problem.Extensions["code"] = code;
        if (errors is not null)
            problem.Extensions["errors"] = errors;
        foreach (var (key, value) in extensions ?? new Dictionary<string, object?>())
            problem.Extensions[key] = value;
        return problem;
    }
}
