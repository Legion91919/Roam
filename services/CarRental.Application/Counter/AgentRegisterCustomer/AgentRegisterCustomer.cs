using CarRental.Application.Abstractions.Identity;
using CarRental.Application.Abstractions.Messaging;
using CarRental.Application.Abstractions.Persistence;
using CarRental.Domain.Common;
using CarRental.Domain.Customers;
using FluentValidation;
using MediatR;

namespace CarRental.Application.Counter.AgentRegisterCustomer;

/// <summary>
/// An agent registers a walk-in customer at the counter: a sign-in account plus the driver profile.
/// The email here is the NEW customer's (the agent's own identity still comes from the token, and
/// the API only lets the Agent role send this). There is no loyalty tier: tiers are set by the business.
/// </summary>
public sealed record AgentRegisterCustomerCommand(
    string Email,
    string FirstName,
    string LastName,
    string PhoneNumber,
    IdentityDocumentType DocumentType,
    string DocumentNumber,
    string? IssuingCountry,
    DateOnly DateOfBirth,
    DateOnly LicenceIssuedOn) : ICommand<AgentRegisteredCustomerDto>;

/// <summary>
/// <see cref="TemporaryPassword"/> is shown to the agent ONCE (read it out or print it). Production
/// would email the customer an invitation link to set their own password instead.
/// </summary>
public sealed record AgentRegisteredCustomerDto(Guid CustomerId, string Email, string TemporaryPassword);

public sealed class AgentRegisterCustomerValidator : AbstractValidator<AgentRegisterCustomerCommand>
{
    public AgentRegisterCustomerValidator()
    {
        RuleFor(c => c.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(c => c.FirstName).NotEmpty().MaximumLength(60);
        RuleFor(c => c.LastName).NotEmpty().MaximumLength(60);
        RuleFor(c => c.PhoneNumber).NotEmpty().MaximumLength(20);
        RuleFor(c => c.DocumentType).IsInEnum();
        RuleFor(c => c.DocumentNumber).NotEmpty().MaximumLength(20);
    }
}

/// <summary>
/// Order matters, because the sign-in account lives in a different store (Identity) from the
/// customer (one SaveChanges cannot cover both):
///   1. Check EVERY customer rule first (document, DOB matches the SA ID, licence dates, one
///      document per account), so a rejected walk-in never leaves an orphan account behind.
///   2. Create the account (fails with account.email_in_use if the email is taken).
///   3. Register the Customer aggregate for that account and save.
/// </summary>
public sealed class AgentRegisterCustomerHandler(
    IUserAccounts accounts,
    ICustomerRepository customers,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
    : IRequestHandler<AgentRegisterCustomerCommand, AgentRegisteredCustomerDto>
{
    public async Task<AgentRegisteredCustomerDto> Handle(AgentRegisterCustomerCommand command, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var email = command.Email.Trim();

        // 1. All customer rules, before anything is created
        var document = command.DocumentType == IdentityDocumentType.SouthAfricanId
            ? IdentityDocument.SouthAfricanId(command.DocumentNumber, today)
            : IdentityDocument.Passport(command.DocumentNumber, command.IssuingCountry ?? string.Empty);
        Customer.EnsureValidDetails(command.FirstName, command.LastName, email, command.PhoneNumber,
            document, command.DateOfBirth, command.LicenceIssuedOn, now);
        if (await customers.DocumentInUseAsync(document, null, cancellationToken))
            throw new DomainException("customer.document_in_use", "This identity document is already linked to another account.");

        // 2. The sign-in account, with a one-time temporary password
        var account = await accounts.CreateUserAsync(email, cancellationToken);

        // 3. The driver profile
        var customer = Customer.Register(account.UserId, command.FirstName, command.LastName, email,
            command.PhoneNumber, document, command.DateOfBirth, command.LicenceIssuedOn, now);
        customers.Add(customer);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AgentRegisteredCustomerDto(customer.Id.Value, email, account.TemporaryPassword);
    }
}
