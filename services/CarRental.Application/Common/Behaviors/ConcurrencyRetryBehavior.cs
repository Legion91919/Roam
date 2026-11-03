using CarRental.Application.Abstractions.Messaging;
using CarRental.Application.Abstractions.Persistence;
using CarRental.Application.Common.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CarRental.Application.Common.Behaviors;

/// <summary>
/// Re-runs a command when it loses an optimistic concurrency race.
/// </summary>
/// <remarks>
/// Optimistic concurrency means "don't lock; detect the clash at save time". The cost is that a
/// request can lose a race even when there was room for both (two people booking different cars
/// of the same category on the same day touch the same inventory row). Retrying with FRESH data
/// fixes that: the second attempt sees the updated Reserved count, and either succeeds or fails
/// with a genuine "sold out".
///
/// Only commands marked <see cref="IRetryOnConcurrencyConflict"/> are retried (they must be safe
/// to repeat). After <see cref="MaxAttempts"/> the exception bubbles up and the API returns 409.
/// Random jitter stops the losers from colliding again in lock-step.
/// </remarks>
public sealed class ConcurrencyRetryBehavior<TRequest, TResponse>(
    IUnitOfWork unitOfWork,
    ILogger<ConcurrencyRetryBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public const int MaxAttempts = 3;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not IRetryOnConcurrencyConflict)
            return await next();

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await next();
            }
            catch (ConcurrencyConflictException) when (attempt < MaxAttempts)
            {
                logger.LogWarning("Concurrency conflict on {Request}, attempt {Attempt} of {Max}; retrying with fresh data",
                    typeof(TRequest).Name, attempt, MaxAttempts);

                unitOfWork.DiscardChanges();
                await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(10, 50) * attempt), cancellationToken);
            }
        }
    }
}
