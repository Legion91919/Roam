using System.Collections.Concurrent;
using CarRental.Application.Abstractions.Payments;
using CarRental.Domain.ValueObjects;

namespace CarRental.Infrastructure.Payments;

/// <summary>
/// ADAPTER for IPaymentGateway that behaves like a card gateway's sandbox, with no network calls.
/// Deterministic by test token, so demos and tests always get the same answer:
///   tok_visa_approve, tok_mc_approve  approved
///   tok_decline                       declined, card_declined
///   tok_insufficient                  declined, insufficient_funds
///   tok_slow                          approved after 2 seconds (to see the loading state)
///   anything else                     declined, invalid_token
/// </summary>
/// <remarks>
/// A real provider (Peach Payments, PayFast, Stripe, Adyen) is another class implementing the same
/// port, selected with Payments:Provider. No use case changes: they only know IPaymentGateway.
///
/// Idempotency: the first answer for each key is remembered, so a retried request gets the same
/// result and never creates a second hold or capture. (A real gateway keeps this server-side.)
/// Captures and voids are approved for any reference this mock issued, which keeps working after
/// the API restarts.
/// </remarks>
public sealed class MockPaymentGateway : IPaymentGateway
{
    public const string ReferencePrefix = "mock_auth_";
    public static readonly TimeSpan SlowDelay = TimeSpan.FromSeconds(2);

    private readonly ConcurrentDictionary<string, GatewayResult> _byIdempotencyKey = new();

    /// <summary>How many distinct authorisations were placed. Lets tests prove a retry did not double-charge.</summary>
    public int AuthorizationCount { get; private set; }

    private static readonly Dictionary<string, (bool Approved, string? Reason, string Brand, string Last4)> Tokens = new()
    {
        ["tok_visa_approve"] = (true, null, "Visa", "4242"),
        ["tok_mc_approve"] = (true, null, "Mastercard", "4444"),
        ["tok_decline"] = (false, "card_declined", "Visa", "0002"),
        ["tok_insufficient"] = (false, "insufficient_funds", "Visa", "9995"),
        ["tok_slow"] = (true, null, "Visa", "0077"),
    };

    public async Task<GatewayResult> AuthorizeAsync(string paymentToken, Money amount, string idempotencyKey, CancellationToken cancellationToken)
    {
        if (_byIdempotencyKey.TryGetValue(idempotencyKey, out var previous))
            return previous;

        if (paymentToken == "tok_slow")
            await Task.Delay(SlowDelay, cancellationToken);

        var result = Tokens.TryGetValue(paymentToken, out var card)
            ? card.Approved
                ? new GatewayResult(GatewayDecision.Approved, ReferencePrefix + Guid.NewGuid().ToString("N"), null, card.Brand, card.Last4)
                : new GatewayResult(GatewayDecision.Declined, null, card.Reason, card.Brand, card.Last4)
            : new GatewayResult(GatewayDecision.Declined, null, "invalid_token", null, null);

        var stored = _byIdempotencyKey.GetOrAdd(idempotencyKey, result);
        if (ReferenceEquals(stored, result) && result.Approved)
            AuthorizationCount++;
        return stored;
    }

    public Task<GatewayResult> CaptureAsync(string gatewayReference, Money amount, string idempotencyKey, CancellationToken cancellationToken) =>
        Task.FromResult(_byIdempotencyKey.GetOrAdd(idempotencyKey, _ => Settle(gatewayReference, "capture")));

    public Task<GatewayResult> VoidAsync(string gatewayReference, string idempotencyKey, CancellationToken cancellationToken) =>
        Task.FromResult(_byIdempotencyKey.GetOrAdd(idempotencyKey, _ => Settle(gatewayReference, "void")));

    private static GatewayResult Settle(string gatewayReference, string operation) =>
        gatewayReference.StartsWith(ReferencePrefix, StringComparison.Ordinal)
            ? new GatewayResult(GatewayDecision.Approved, $"mock_{operation}_{Guid.NewGuid():N}", null, null, null)
            : new GatewayResult(GatewayDecision.Declined, null, "unknown_authorization", null, null);
}
