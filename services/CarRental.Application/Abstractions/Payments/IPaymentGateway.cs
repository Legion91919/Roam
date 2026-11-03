using CarRental.Domain.Identifiers;
using CarRental.Domain.ValueObjects;

namespace CarRental.Application.Abstractions.Payments;

/// <summary>
/// PORT: a card payment gateway (PayFast, Peach, Stripe, Adyen...). Implemented in Infrastructure.
/// </summary>
/// <remarks>
/// It only ever receives a TOKEN, never a card number: the browser sends the card details straight
/// to the gateway's hosted fields, which return the token. That keeps card data out of our Domain,
/// database and logs (PCI DSS scope).
///
/// Every call takes an idempotency key. If a request is retried (lost race, timeout, client retry)
/// with the same key, the gateway returns the first answer instead of charging again.
/// </remarks>
public interface IPaymentGateway
{
    /// <summary>Place a hold for <paramref name="amount"/> on the tokenised card.</summary>
    Task<GatewayResult> AuthorizeAsync(string paymentToken, Money amount, string idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Take <paramref name="amount"/> from an earlier authorisation.</summary>
    Task<GatewayResult> CaptureAsync(string gatewayReference, Money amount, string idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Release an authorisation without taking any money.</summary>
    Task<GatewayResult> VoidAsync(string gatewayReference, string idempotencyKey, CancellationToken cancellationToken);
}

public enum GatewayDecision
{
    Approved,
    Declined
}

/// <summary>
/// The gateway's answer. <see cref="ReasonCode"/> explains a decline ("card_declined",
/// "insufficient_funds"). The card brand and last four digits come from the gateway, not the browser
/// (null when the gateway could not read the token).
/// </summary>
public sealed record GatewayResult(GatewayDecision Decision, string? Reference, string? ReasonCode, string? CardBrand, string? CardLast4)
{
    public bool Approved => Decision == GatewayDecision.Approved;
}

/// <summary>
/// Idempotency keys are derived from the reservation id, so a retried booking, cancellation or
/// return re-uses the same key and the gateway never charges or releases twice.
/// </summary>
public static class PaymentIdempotencyKey
{
    public static string Authorize(ReservationId id) => $"{id.Value:N}:authorize";
    public static string Capture(ReservationId id) => $"{id.Value:N}:capture";
    public static string Void(ReservationId id) => $"{id.Value:N}:void";

    /// <summary>
    /// Pay at the branch: one key per card the agent tries (the token is single-use), so a retry of the
    /// same attempt is idempotent but a second card after a decline is a new authorisation. The token is
    /// hashed: keys end up in gateway logs.
    /// </summary>
    public static string AuthorizeAtCounter(ReservationId id, string paymentToken) =>
        $"{id.Value:N}:authorize:counter:{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(paymentToken)))[..16]}";
}
