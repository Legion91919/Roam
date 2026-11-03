using CarRental.Domain.Common;
using CarRental.Domain.ValueObjects;

namespace CarRental.Domain.Tests.ValueObjects;

public class MoneyTests
{
    [Fact]
    public void Money_is_compared_by_value()
    {
        Assert.Equal(Money.Zar(100m), new Money(100m, "zar"));
    }

    [Fact]
    public void Adding_different_currencies_is_rejected()
    {
        var ex = Assert.Throws<DomainException>(() => Money.Zar(10m) + new Money(10m, "USD"));
        Assert.Equal("money.currency_mismatch", ex.Code);
    }

    [Theory]
    [InlineData(1978.375, 1978.38)]
    [InlineData(0.005, 0.01)]
    [InlineData(-0.005, -0.01)]
    public void Rounding_is_to_cents_away_from_zero(decimal amount, decimal expected)
    {
        Assert.Equal(expected, Money.Zar(amount).Round().Amount);
    }

    [Fact]
    public void Decimal_keeps_cents_exact_where_double_would_drift()
    {
        var total = Money.Zero();
        for (var i = 0; i < 10; i++) total += Money.Zar(0.10m);

        Assert.Equal(1.00m, total.Amount);
        Assert.NotEqual(1.0, Enumerable.Repeat(0.1, 10).Sum());   // the double version is 0.9999999999999999
    }

    [Fact]
    public void Operations_return_new_instances()
    {
        var price = Money.Zar(100m);
        var doubled = price * 2;

        Assert.Equal(100m, price.Amount);
        Assert.Equal(200m, doubled.Amount);
    }
}
