using CarRental.Application.Abstractions.Identity;
using CarRental.Application.Abstractions.Messaging;
using CarRental.Application.Abstractions.Persistence;
using CarRental.Application.Common.Exceptions;
using CarRental.Domain.Common;
using CarRental.Domain.Customers;
using FluentValidation;
using MediatR;

namespace CarRental.Application.Customers.SaveMyProfile;

/// <summary>
/// Create or update the signed-in user's driver profile. Email comes from the sign-in, not the body,
/// and there is deliberately no loyalty tier here: customers cannot grant themselves discounts.
/// </summary>
public sealed record SaveMyProfileCommand(
    string FirstName,
    string LastName,
    string PhoneNumber,
    IdentityDocumentType DocumentType,
    string DocumentNumber,
    string? IssuingCountry,
    DateOnly DateOfBirth,
    DateOnly LicenceIssuedOn) : ICommand<Guid>;

public sealed class SaveMyProfileValidator : AbstractValidator<SaveMyProfileCommand>
{
    public SaveMyProfileValidator()
    {
        RuleFor(c => c.FirstName).NotEmpty().MaximumLength(60);
        RuleFor(c => c.LastName).NotEmpty().MaximumLength(60);
        RuleFor(c => c.PhoneNumber).NotEmpty().MaximumLength(20);
        RuleFor(c => c.DocumentType).IsInEnum();
        RuleFor(c => c.DocumentNumber).NotEmpty().MaximumLength(20);
    }
}

public sealed class SaveMyProfileHandler(
    ICurrentUser user,
    ICustomerRepository customers,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
    : IRequestHandler<SaveMyProfileCommand, Guid>
{
    public async Task<Guid> Handle(SaveMyProfileCommand command, CancellationToken cancellationToken)
    {
        var userId = user.UserId ?? throw new UnauthenticatedException();
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var document = command.DocumentType == IdentityDocumentType.SouthAfricanId
            ? IdentityDocument.SouthAfricanId(command.DocumentNumber, today)
            : IdentityDocument.Passport(command.DocumentNumber, command.IssuingCountry ?? string.Empty);

        var customer = await customers.GetByUserIdAsync(userId, cancellationToken);

        if (await customers.DocumentInUseAsync(document, customer?.Id, cancellationToken))
            throw new DomainException("customer.document_in_use", "This identity document is already linked to another account.");

        if (customer is null)
        {
            customer = Customer.Register(userId, command.FirstName, command.LastName, user.Email ?? string.Empty,
                command.PhoneNumber, document, command.DateOfBirth, command.LicenceIssuedOn, now);
            customers.Add(customer);
        }
        else
        {
            customer.UpdateDetails(command.FirstName, command.LastName, user.Email ?? customer.Email,
                command.PhoneNumber, document, command.DateOfBirth, command.LicenceIssuedOn, now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return customer.Id.Value;
    }
}
