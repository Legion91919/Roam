using FluentValidation;
using MediatR;

namespace CarRental.Application.Common.Behaviors;

/// <summary>
/// Runs every FluentValidation validator registered for the request BEFORE the handler.
/// </summary>
/// <remarks>
/// Two kinds of "invalid", deliberately handled in two places:
///  * SHAPE (validators, here): missing IDs, return before pickup, unknown enum. Cheap checks,
///    no database. Fails fast with HTTP 400 and a list of field errors.
///  * BUSINESS RULES (Domain): under-age driver, sold out, odometer going backwards. These need the
///    domain model and fail with 422/409. The Domain re-checks what it relies on anyway, so it is
///    never dependent on a validator having run.
/// </remarks>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, cancellationToken)));
        var failures = results.SelectMany(r => r.Errors).Where(f => f is not null).ToList();

        if (failures.Count > 0)
            throw new ValidationException(failures);

        return await next();
    }
}
