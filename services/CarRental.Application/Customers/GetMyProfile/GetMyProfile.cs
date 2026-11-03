using CarRental.Application.Abstractions.Identity;
using CarRental.Application.Abstractions.Messaging;
using CarRental.Application.Abstractions.ReadModels;
using CarRental.Application.Common.Exceptions;
using MediatR;

namespace CarRental.Application.Customers.GetMyProfile;

public sealed record GetMyProfileQuery : IQuery<CustomerProfileDto?>;

/// <summary>The ID number is never returned in full: only <see cref="MaskedDocumentNumber"/>.</summary>
public sealed record CustomerProfileDto(
    Guid CustomerId,
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber,
    string DocumentType,
    string MaskedDocumentNumber,
    string IssuingCountry,
    DateOnly DateOfBirth,
    DateOnly LicenceIssuedOn,
    string LoyaltyTier);

public sealed class GetMyProfileHandler(ICurrentUser user, ICustomerReadModel readModel) : IRequestHandler<GetMyProfileQuery, CustomerProfileDto?>
{
    public Task<CustomerProfileDto?> Handle(GetMyProfileQuery query, CancellationToken cancellationToken) =>
        readModel.GetByUserIdAsync(user.UserId ?? throw new UnauthenticatedException(), cancellationToken);
}
