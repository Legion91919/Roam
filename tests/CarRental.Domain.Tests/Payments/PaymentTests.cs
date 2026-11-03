using CarRental.Domain.Common;
using CarRental.Domain.Identifiers;
using CarRental.Domain.Payments;
using CarRental.Domain.ValueObjects;
using static CarRental.Domain.Tests.TestData;

namespace CarRental.Domain.Tests.Payments;

public class PaymentTests
{
    private static readonly CardSummary Visa = new("Visa", "4242");

    private static Payment Authorized(decimal amount = 1000m) =>
        Payment.Authorize(ReservationId.New(), Money.Zar(amount), Visa, approved: true, "auth_123", null, Now);

    [Fact]
    public void An_approved_authorisation_holds_the_amount_and_raises_PaymentAuthorized()
    {
        var payment = Authorized(1050m);

        Assert.Equal(PaymentStatus.Authorized, payment.Status);
        Assert.Equal(Money.Zar(1050m), payment.Authorized);
        Assert.Equal(Money.Zar(0m), payment.Captured);
        Assert.Equal("auth_123", payment.GatewayReference);
        Assert.IsType<PaymentAuthorized>(Assert.Single(payment.DomainEvents));
    }

    [Fact]
    public void A_declined_authorisation_throws_payment_declined_with_the_reason()
    {
        var ex = Assert.Throws<PaymentDeclinedException>(() =>
            Payment.Authorize(ReservationId.New(), Money.Zar(500m), Visa, approved: false, null, "insufficient_funds", Now));

        Assert.Equal("payment.declined", ex.Code);
        Assert.Equal("insufficient_funds", ex.Reason);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Amounts_must_be_positive(decimal amount)
    {
        var ex = Assert.Throws<DomainException>(() => Authorized(amount));
        Assert.Equal("payment.invalid_amount", ex.Code);
    }

    [Fact]
    public void An_approved_authorisation_needs_a_gateway_reference()
    {
        var ex = Assert.Throws<DomainException>(() =>
            Payment.Authorize(ReservationId.New(), Money.Zar(500m), Visa, approved: true, " ", null, Now));
        Assert.Equal("payment.gateway_reference_required", ex.Code);
    }

    [Fact]
    public void Capture_takes_the_money_and_raises_PaymentCaptured()
    {
        var payment = Authorized(1000m);

        payment.Capture(Money.Zar(1000m), approved: true, null, Now);

        Assert.Equal(PaymentStatus.Captured, payment.Status);
        Assert.Equal(Money.Zar(1000m), payment.Captured);
        Assert.Contains(payment.DomainEvents, e => e is PaymentCaptured);
    }

    [Fact]
    public void Capture_may_exceed_the_authorisation_by_up_to_15_percent_for_late_fees()
    {
        var payment = Authorized(1000m);

        payment.Capture(Money.Zar(1150m), approved: true, null, Now);

        Assert.Equal(Money.Zar(1150m), payment.Captured);
    }

    [Fact]
    public void Capture_more_than_15_percent_above_the_authorisation_is_refused()
    {
        var payment = Authorized(1000m);

        var ex = Assert.Throws<DomainException>(() => payment.Capture(Money.Zar(1150.01m), approved: true, null, Now));

        Assert.Equal("payment.capture_exceeds_authorization", ex.Code);
        Assert.Equal(PaymentStatus.Authorized, payment.Status);
    }

    [Fact]
    public void Capture_may_be_less_than_the_authorisation()
    {
        var payment = Authorized(1000m);

        payment.Capture(Money.Zar(900m), approved: true, null, Now);

        Assert.Equal(Money.Zar(900m), payment.Captured);
    }

    [Fact]
    public void Capture_must_be_in_the_same_currency_as_the_authorisation()
    {
        var payment = Authorized(1000m);

        var ex = Assert.Throws<DomainException>(() => payment.Capture(new Money(1000m, "USD"), approved: true, null, Now));
        Assert.Equal("money.currency_mismatch", ex.Code);
    }

    [Fact]
    public void A_declined_capture_leaves_the_payment_failed_with_the_reason()
    {
        var payment = Authorized(1000m);

        payment.Capture(Money.Zar(1000m), approved: false, "card_declined", Now);

        Assert.Equal(PaymentStatus.Failed, payment.Status);
        Assert.Equal("card_declined", payment.FailureReason);
        Assert.Contains(payment.DomainEvents, e => e is PaymentFailed);
    }

    [Fact]
    public void Void_releases_the_hold_and_raises_PaymentVoided()
    {
        var payment = Authorized(1000m);

        payment.Void(approved: true, null, Now);

        Assert.Equal(PaymentStatus.Voided, payment.Status);
        Assert.Equal(Money.Zar(0m), payment.Captured);
        Assert.Contains(payment.DomainEvents, e => e is PaymentVoided);
    }

    [Fact]
    public void Captured_payment_cannot_be_captured_again_or_voided()
    {
        var payment = Authorized(1000m);
        payment.Capture(Money.Zar(1000m), approved: true, null, Now);

        Assert.Equal("payment.invalid_transition", Assert.Throws<DomainException>(() => payment.Capture(Money.Zar(10m), true, null, Now)).Code);
        Assert.Equal("payment.invalid_transition", Assert.Throws<DomainException>(() => payment.Void(true, null, Now)).Code);
    }

    [Fact]
    public void Voided_payment_cannot_be_captured_or_voided_again()
    {
        var payment = Authorized(1000m);
        payment.Void(approved: true, null, Now);

        Assert.Equal("payment.invalid_transition", Assert.Throws<DomainException>(() => payment.Capture(Money.Zar(1000m), true, null, Now)).Code);
        Assert.Equal("payment.invalid_transition", Assert.Throws<DomainException>(() => payment.Void(true, null, Now)).Code);
    }

    [Fact]
    public void Failed_payment_is_final()
    {
        var payment = Authorized(1000m);
        payment.Void(approved: false, "void_declined", Now);

        Assert.Equal(PaymentStatus.Failed, payment.Status);
        Assert.Equal("payment.invalid_transition", Assert.Throws<DomainException>(() => payment.Capture(Money.Zar(1000m), true, null, Now)).Code);
        Assert.Equal("payment.invalid_transition", Assert.Throws<DomainException>(() => payment.Void(true, null, Now)).Code);
    }

    [Fact]
    public void Capture_limit_is_the_authorisation_plus_15_percent_rounded_to_cents()
    {
        Assert.Equal(Money.Zar(1207.50m), Authorized(1050m).CaptureLimit);
        Assert.Equal(Money.Zar(4105.50m), Authorized(3570m).CaptureLimit);
    }

    [Theory]
    [InlineData("Visa", "424")]
    [InlineData("Visa", "42424")]
    [InlineData("Visa", "42a2")]
    [InlineData("", "4242")]
    public void A_card_summary_holds_a_brand_and_exactly_four_digits(string brand, string last4)
    {
        var ex = Assert.Throws<DomainException>(() => new CardSummary(brand, last4));
        Assert.Equal("payment.invalid_card_summary", ex.Code);
    }
}
