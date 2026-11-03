using CarRental.Application.Abstractions.Identity;
using CarRental.Application.Abstractions.Persistence;
using CarRental.Application.Common.Exceptions;
using CarRental.Domain.Common;
using CarRental.Domain.Customers;

namespace CarRental.Application.Common.Customers;

/// <summary>
/// Resolves the signed-in user's Customer aggregate for commands. One place for the two rules
/// every customer use case shares: you must be signed in (401), and you must have a profile (422).
/// </summary>
public sealed class CurrentCustomer(ICurrentUser user, ICustomerRepository customers)
{
    public string RequireUserId() => user.UserId ?? throw new UnauthenticatedException();

    public async Task<Customer> RequireAsync(CancellationToken cancellationToken) =>
        await customers.GetByUserIdAsync(RequireUserId(), cancellationToken)
        ?? throw new DomainException("customer.profile_required", "Complete your driver profile before booking.");
}
