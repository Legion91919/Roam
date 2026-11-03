using CarRental.Domain.ValueObjects;

namespace CarRental.Domain.Pricing.Rules;

/// <summary>7 or more chargeable days: 15% off the base rental.</summary>
// SOLID (S): one business rule, one reason to change. (O): added without touching RentalPricer.
public sealed class WeeklyDiscountRule : IPriceRule
{
    public const int MinimumDays = 7;
    public const decimal Percent = 0.15m;

    public int Order => 100;

    public void Apply(PriceContext context)
    {
        if (context.ChargeableDays >= MinimumDays)
            context.ApplyDiscount("weekly_discount", "Weekly discount (15%)", Percent);
    }
}

/// <summary>Loyalty discount, applied AFTER the weekly discount (so they compound, not add).</summary>
// SOLID (S): one business rule, one reason to change. (O): added without touching RentalPricer.
public sealed class LoyaltyDiscountRule : IPriceRule
{
    public int Order => 110;

    public void Apply(PriceContext context)
    {
        switch (context.Request.Driver.Tier)
        {
            case LoyaltyTier.Gold:
                context.ApplyDiscount("loyalty_gold", "Gold member discount (10%)", 0.10m);
                break;
            case LoyaltyTier.Silver:
                context.ApplyDiscount("loyalty_silver", "Silver member discount (5%)", 0.05m);
                break;
        }
    }
}

/// <summary>Drivers under 25 on the pickup date pay a daily surcharge (insurance risk).</summary>
// SOLID (S): one business rule, one reason to change. (O): added without touching RentalPricer.
public sealed class YoungDriverFeeRule : IPriceRule
{
    public const int AgeThreshold = 25;
    public static readonly Money DailyFee = Money.Zar(150m);

    public int Order => 300;

    public void Apply(PriceContext context)
    {
        if (context.Request.Driver.AgeOn(context.Request.PickupDate) < AgeThreshold)
            context.AddFee("young_driver", $"Young driver fee ({context.ChargeableDays} x {DailyFee})", DailyFee * context.ChargeableDays);
    }
}

/// <summary>Returning to a different branch costs a flat fee (someone has to bring the car back).</summary>
// SOLID (S): one business rule, one reason to change. (O): added without touching RentalPricer.
public sealed class OneWayFeeRule : IPriceRule
{
    public static readonly Money Fee = Money.Zar(750m);

    public int Order => 310;

    public void Apply(PriceContext context)
    {
        if (context.Request.PickupBranchId != context.Request.ReturnBranchId)
            context.AddFee("one_way", "One-way fee", Fee);
    }
}

/// <summary>Extras are charged per day with a cap per rental. Never discounted.</summary>
// SOLID (S): one business rule, one reason to change. (O): added without touching RentalPricer.
public sealed class ExtrasRule : IPriceRule
{
    public static readonly Money GpsPerDay = Money.Zar(60m);
    public static readonly Money GpsCap = Money.Zar(300m);
    public static readonly Money ChildSeatPerDay = Money.Zar(50m);
    public static readonly Money ChildSeatCap = Money.Zar(250m);

    public int Order => 320;

    public void Apply(PriceContext context)
    {
        var days = context.ChargeableDays;
        if (context.Request.Extras.HasFlag(Extras.Gps))
            context.AddFee("extra_gps", "GPS", Money.Min(GpsPerDay * days, GpsCap));
        if (context.Request.Extras.HasFlag(Extras.ChildSeat))
            context.AddFee("extra_child_seat", "Child seat", Money.Min(ChildSeatPerDay * days, ChildSeatCap));
    }
}
