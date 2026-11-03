using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CarRental.Application.Common.Behaviors;

/// <summary>
/// PIPELINE BEHAVIOUR (Decorator pattern): runs around EVERY request. Cross-cutting concerns
/// (logging, validation, retries, transactions) live here once, instead of being copy-pasted into
/// every handler. Handlers stay focused on the use case (SOLID S).
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var name = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var response = await next();
            logger.LogInformation("{Request} handled in {ElapsedMs} ms", name, stopwatch.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogWarning("{Request} failed after {ElapsedMs} ms: {Error}", name, stopwatch.ElapsedMilliseconds, ex.Message);
            throw;
        }
    }
}
