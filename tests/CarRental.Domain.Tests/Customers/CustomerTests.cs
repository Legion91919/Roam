using CarRental.Domain.Common;
using CarRental.Domain.Customers;
using CarRental.Domain.ValueObjects;
using static CarRental.Domain.Tests.TestData;

namespace CarRental.Domain.Tests.Customers;

public class CustomerTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now.UtcDateTime);
    private static IdentityDocument SaId(string number = "9005105009088") => IdentityDocument.SouthAfricanId(number, Today);

    private static Customer Register(IdentityDocument? document = null, DateOnly? dateOfBirth = null, DateOnly? licence = null, string email = "thabo@example.com") =>
        Customer.Register("user-1", "Thabo", "Nkosi", email, "+27 82 459 1102",
            document ?? SaId(), dateOfBirth ?? new DateOnly(1990, 5, 10), licence ?? new DateOnly(2010, 1, 15), Now);

    [Fact]
    public void A_valid_customer_registers_with_no_loyalty_tier()
    {
        var customer = Register();

        Assert.Equal("Thabo Nkosi", customer.FullName);
        Assert.Equal(LoyaltyTier.None, customer.LoyaltyTier);
        Assert.Equal(new DateOnly(1990, 5, 10), customer.ToDriverProfile().DateOfBirth);
    }

    [Fact]
    public void Date_of_birth_must_match_the_SA_ID_number()
    {
        // Someone born in 2003 claiming to be born in 1990 to dodge the age rules.
        var ex = Assert.Throws<DomainException>(() => Register(SaId("0308125800083"), new DateOnly(1990, 5, 10)));
        Assert.Equal("customer.birth_date_mismatch", ex.Code);
    }

    [Fact]
    public void Passports_have_no_encoded_birth_date_so_any_consistent_date_is_accepted()
    {
        var customer = Register(IdentityDocument.Passport("c01x00t47", "gbr"), new DateOnly(1988, 3, 1), new DateOnly(2008, 6, 1));

        Assert.Equal("C01X00T47", customer.Document.Number);
        Assert.Equal("GBR", customer.Document.IssuingCountry);
    }

    [Fact]
    public void Invalid_SA_ID_numbers_are_rejected()
    {
        var ex = Assert.Throws<DomainException>(() => SaId("9005105009089"));
        Assert.Equal("identity.invalid_sa_id", ex.Code);
    }

    [Theory]
    [InlineData("12345", "GBR", "identity.invalid_passport")]
    [InlineData("C01X00T47", "UK", "identity.invalid_country")]
    public void Invalid_passports_are_rejected(string number, string country, string code)
    {
        Assert.Equal(code, Assert.Throws<DomainException>(() => IdentityDocument.Passport(number, country)).Code);
    }

    [Fact]
    public void Licence_cannot_be_issued_in_the_future()
    {
        var ex = Assert.Throws<DomainException>(() => Register(licence: Today.AddDays(1)));
        Assert.Equal("driver.licence_in_future", ex.Code);
    }

    [Fact]
    public void Email_must_be_valid()
    {
        Assert.Equal("customer.invalid_email", Assert.Throws<DomainException>(() => Register(email: "not-an-email")).Code);
    }

    [Fact]
    public void The_ID_number_is_only_ever_shown_masked()
    {
        Assert.Equal("•••••••••9088", SaId().Masked);
    }

    [Fact]
    public void Loyalty_tier_is_set_by_the_business_and_flows_into_pricing()
    {
        var customer = Register();
        customer.SetLoyaltyTier(LoyaltyTier.Gold);

        Assert.Equal(LoyaltyTier.Gold, customer.ToDriverProfile().Tier);
    }
}
