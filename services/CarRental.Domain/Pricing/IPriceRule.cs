namespace CarRental.Domain.Pricing;

/// <summary>
/// STRATEGY + OPEN/CLOSED PRINCIPLE: each pricing rule is a small class.
/// </summary>
/// <remarks>
/// Adding a promotion means adding one class and registering it in DI. RentalPricer and the
/// existing rules do not change (open for extension, closed for modification), and each rule
/// can be unit tested on its own.
///
/// Why an explicit <see cref="Order"/>? Discounts compound and must run before fees, and the order
/// should not depend on the order of lines in a DI registration file. Convention used here:
/// 100s = discounts on the base, 300s+ = fees.
/// </remarks>
// SOLID (O) Open/Closed: new pricing behaviour = new IPriceRule class, no edits to existing code.
// SOLID (L) Liskov: every rule must honour the same contract: it may only change the price through
//           PriceContext.ApplyDiscount/AddFee and must never throw for a valid request, so the pricer
//           can run ANY rule without special cases. Enforced by PriceRuleContractTests.
// SOLID (I) Interface Segregation: one tiny member to implement, nothing a rule does not need.
public interface IPriceRule
{
    int Order { get; }
    void Apply(PriceContext context);
}
