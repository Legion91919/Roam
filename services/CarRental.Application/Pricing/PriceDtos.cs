using CarRental.Domain.Pricing;

namespace CarRental.Application.Pricing;

public sealed record PriceLineDto(string Code, string Description, decimal Amount);

public sealed record QuoteDto(int ChargeableDays, decimal DailyRate, decimal Total, string Currency, IReadOnlyList<PriceLineDto> Lines)
{
    /// <summary>Map the domain quote to a DTO so domain types never leak into the HTTP contract.</summary>
    public static QuoteDto From(PriceQuote quote) => new(
        quote.ChargeableDays,
        quote.DailyRate.Amount,
        quote.Total.Amount,
        quote.Total.Currency,
        quote.Lines.Select(l => new PriceLineDto(l.Code, l.Description, l.Amount.Amount)).ToList());
}
