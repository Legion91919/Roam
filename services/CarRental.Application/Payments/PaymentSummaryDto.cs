using CarRental.Domain.Payments;

namespace CarRental.Application.Payments;

/// <summary>What screens show about a payment: status and "Visa •••• 4242". Never more card data than that.</summary>
public sealed record PaymentSummaryDto(
    string Status,
    string CardBrand,
    string CardLast4,
    decimal Authorized,
    decimal Captured,
    string Currency,
    string? FailureReason)
{
    public static PaymentSummaryDto From(Payment payment) => new(
        payment.Status.ToString(), payment.Card.Brand, payment.Card.Last4,
        payment.Authorized.Amount, payment.Captured.Amount, payment.Authorized.Currency, payment.FailureReason);
}
