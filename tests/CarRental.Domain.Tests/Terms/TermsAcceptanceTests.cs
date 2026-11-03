using CarRental.Domain.Common;
using CarRental.Domain.Terms;
using static CarRental.Domain.Tests.TestData;

namespace CarRental.Domain.Tests.Terms;

public class TermsAcceptanceTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Booking_without_accepting_terms_is_rejected(string? accepted)
    {
        var ex = Assert.Throws<DomainException>(() => TermsAcceptance.Accept(accepted, TermsVersion, Now));
        Assert.Equal("terms.not_accepted", ex.Code);
    }

    [Fact]
    public void Accepting_an_old_version_is_rejected()
    {
        var ex = Assert.Throws<DomainException>(() => TermsAcceptance.Accept("2025-01-01", TermsVersion, Now));
        Assert.Equal("terms.outdated", ex.Code);
    }

    [Fact]
    public void Acceptance_records_the_version_and_time()
    {
        var acceptance = TermsAcceptance.Accept(TermsVersion, TermsVersion, Now);

        Assert.Equal(TermsVersion, acceptance.Version);
        Assert.Equal(Now, acceptance.AcceptedAt);
    }
}
