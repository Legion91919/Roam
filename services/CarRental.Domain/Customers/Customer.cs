using System.Net.Mail;
using CarRental.Domain.Common;
using CarRental.Domain.Identifiers;
using CarRental.Domain.ValueObjects;

namespace CarRental.Domain.Customers;

/// <summary>
/// AGGREGATE ROOT: a registered customer and main driver.
/// </summary>
/// <remarks>
/// The Domain doesn't know HOW people sign in. <see cref="UserId"/> is just the opaque subject id of
/// the authenticated user (ASP.NET Core Identity today, Entra ID tomorrow: Domain unchanged).
///
/// Invariants:
///  * Names, email and phone are present and sensible.
///  * The identity document is valid (SA ID checksum and date, or passport format).
///  * For a South African ID, the date of birth MUST match the one encoded in the ID number
///    (stops a 19-year-old typing a 1990 birthday to dodge the age rules).
///  * A licence can't be issued before birth or in the future.
///  * The loyalty tier is set by the business, never by the customer (see <see cref="SetLoyaltyTier"/>).
/// </remarks>
public sealed class Customer : AggregateRoot<CustomerId>
{
    public string UserId { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string PhoneNumber { get; private set; } = string.Empty;
    public IdentityDocument Document { get; private set; } = null!;
    public DateOnly DateOfBirth { get; private set; }
    public DateOnly LicenceIssuedOn { get; private set; }
    public LoyaltyTier LoyaltyTier { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Customer() { }   // EF Core

    public static Customer Register(
        string userId, string firstName, string lastName, string email, string phoneNumber,
        IdentityDocument document, DateOnly dateOfBirth, DateOnly licenceIssuedOn, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new DomainException("customer.user_required", "A customer must be linked to a signed-in user.");

        var customer = new Customer { Id = CustomerId.New(), UserId = userId, CreatedAt = now, LoyaltyTier = LoyaltyTier.None };
        customer.UpdateDetails(firstName, lastName, email, phoneNumber, document, dateOfBirth, licenceIssuedOn, now);
        return customer;
    }

    public void UpdateDetails(
        string firstName, string lastName, string email, string phoneNumber,
        IdentityDocument document, DateOnly dateOfBirth, DateOnly licenceIssuedOn, DateTimeOffset now)
    {
        EnsureValidDetails(firstName, lastName, email, phoneNumber, document, dateOfBirth, licenceIssuedOn, now);

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Email = email.Trim();
        PhoneNumber = phoneNumber.Trim();
        Document = document;
        DateOfBirth = dateOfBirth;
        LicenceIssuedOn = licenceIssuedOn;
    }

    /// <summary>
    /// Every invariant on a customer's details, without creating one. Lets a use case check a walk-in
    /// customer's details BEFORE it creates their sign-in account (so a rejected ID never leaves an
    /// orphan account behind). <see cref="Register"/> and <see cref="UpdateDetails"/> run the same checks.
    /// </summary>
    public static void EnsureValidDetails(
        string firstName, string lastName, string email, string phoneNumber,
        IdentityDocument document, DateOnly dateOfBirth, DateOnly licenceIssuedOn, DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            throw new DomainException("customer.name_required", "First name and surname are required.");
        if (!MailAddress.TryCreate(email, out _))
            throw new DomainException("customer.invalid_email", "A valid email address is required.");
        var digits = new string(phoneNumber.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length is < 9 or > 15)
            throw new DomainException("customer.invalid_phone", "A valid mobile number is required.");
        if (dateOfBirth > today)
            throw new DomainException("customer.invalid_birth_date", "Date of birth cannot be in the future.");

        var implied = document.ImpliedDateOfBirth(today);
        if (implied is not null && implied != dateOfBirth)
            throw new DomainException("customer.birth_date_mismatch",
                $"Date of birth does not match the ID number (the ID says {implied:yyyy-MM-dd}).");

        if (licenceIssuedOn < dateOfBirth)
            throw new DomainException("driver.licence_before_birth", "Licence issue date cannot be before date of birth.");
        if (licenceIssuedOn > today)
            throw new DomainException("driver.licence_in_future", "Licence issue date cannot be in the future.");
    }

    /// <summary>Set by the loyalty programme (back office or a scheduled job), never from customer input.</summary>
    public void SetLoyaltyTier(LoyaltyTier tier) => LoyaltyTier = tier;

    /// <summary>The snapshot pricing and eligibility need. Copied onto each reservation.</summary>
    public DriverProfile ToDriverProfile() => new(DateOfBirth, LicenceIssuedOn, LoyaltyTier);

    public string FullName => $"{FirstName} {LastName}";
}
