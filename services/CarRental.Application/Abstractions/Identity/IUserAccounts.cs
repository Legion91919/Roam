namespace CarRental.Application.Abstractions.Identity;

/// <summary>
/// PORT: create sign-in accounts on someone else's behalf (an agent registering a walk-in customer).
/// Implemented in Infrastructure with ASP.NET Core Identity's UserManager.
/// </summary>
public interface IUserAccounts
{
    /// <summary>
    /// Creates a user for <paramref name="email"/> with a random one-time temporary password.
    /// Throws DomainException "account.email_in_use" when the email already has an account.
    /// </summary>
    Task<NewUserAccount> CreateUserAsync(string email, CancellationToken cancellationToken);
}

/// <summary>The temporary password is returned ONCE to the agent and never stored in clear or logged.</summary>
public sealed record NewUserAccount(string UserId, string TemporaryPassword);
