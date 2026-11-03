using CarRental.Application.Common.Exceptions;
using CarRental.Application.Customers.SaveMyProfile;
using CarRental.Application.Tests.Fakes;
using CarRental.Domain.Common;
using CarRental.Domain.Customers;
using CarRental.Domain.ValueObjects;
using static CarRental.Application.Tests.Fakes.TestDomain;

namespace CarRental.Application.Tests.Customers;

public class SaveMyProfileHandlerTests
{
    private readonly InMemoryCustomerRepository _customers = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private SaveMyProfileHandler Handler(string? userId = "new-user") =>
        new(new FakeCurrentUser(userId, "lerato@example.com"), _customers, _unitOfWork, new FixedClock(Now));

    private static SaveMyProfileCommand SaId(string number = "9005105009088", DateOnly? dob = null) => new(
        "Lerato", "Dlamini", "+27 82 000 1111", IdentityDocumentType.SouthAfricanId, number, null,
        dob ?? new DateOnly(1990, 5, 10), new DateOnly(2010, 1, 15));

    [Fact]
    public async Task Creates_a_profile_linked_to_the_signed_in_user_with_no_loyalty_tier()
    {
        await Handler().Handle(SaId(), CancellationToken.None);

        var customer = Assert.Single(_customers.Items);
        Assert.Equal("new-user", customer.UserId);
        Assert.Equal("lerato@example.com", customer.Email);   // from the sign-in, not the request
        Assert.Equal(LoyaltyTier.None, customer.LoyaltyTier);
        Assert.Equal(1, _unitOfWork.Commits);
    }

    [Fact]
    public async Task Saving_again_updates_the_same_profile()
    {
        await Handler().Handle(SaId(), CancellationToken.None);
        await Handler().Handle(SaId() with { PhoneNumber = "+27 83 999 0000" }, CancellationToken.None);

        Assert.Equal("+27 83 999 0000", Assert.Single(_customers.Items).PhoneNumber);
    }

    [Fact]
    public async Task Birth_date_that_contradicts_the_ID_number_is_rejected()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() => Handler().Handle(SaId(dob: new DateOnly(1985, 1, 1)), CancellationToken.None));
        Assert.Equal("customer.birth_date_mismatch", ex.Code);
    }

    [Fact]
    public async Task One_identity_document_cannot_be_used_by_two_accounts()
    {
        await Handler("first-user").Handle(SaId(), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DomainException>(() => Handler("second-user").Handle(SaId(), CancellationToken.None));
        Assert.Equal("customer.document_in_use", ex.Code);
    }

    [Fact]
    public async Task Anonymous_users_cannot_save_a_profile()
    {
        await Assert.ThrowsAsync<UnauthenticatedException>(() => Handler(null).Handle(SaId(), CancellationToken.None));
    }
}
