using CarRental.Application.Common.Behaviors;
using CarRental.Application.Common.Customers;
using CarRental.Domain.Pricing;
using CarRental.Domain.Pricing.Rules;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CarRental.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the use cases, the pipeline and the domain services. Called from the Api's
    /// composition root. Each layer registers its own services, so Program.cs stays short.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);

            // Pipeline order = registration order (outermost first):
            //   Logging -> Validation -> ConcurrencyRetry -> Handler
            // Validation sits OUTSIDE the retry loop: an invalid request is never retried.
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(ConcurrencyRetryBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        // ---- Pricing: SOLID (O) + (D) at the composition level ----
        // To add a promotion: write a class implementing IPriceRule and add ONE line here.
        // RentalPricer receives IEnumerable<IPriceRule> and never changes.
        services.AddSingleton<IPriceRule, WeeklyDiscountRule>();
        services.AddSingleton<IPriceRule, LoyaltyDiscountRule>();
        services.AddSingleton<IPriceRule, YoungDriverFeeRule>();
        services.AddSingleton<IPriceRule, OneWayFeeRule>();
        services.AddSingleton<IPriceRule, ExtrasRule>();
        services.AddSingleton(RateCard.Standard);
        services.AddSingleton<RentalPricer>();

        services.AddScoped<CurrentCustomer>();
        services.AddScoped<Reservations.Cancellation.BookingRelease>();

        // No-show fee: SOLID (O): a different policy is a new class and this one line.
        services.AddSingleton<INoShowFeePolicy, OneDayRateNoShowFee>();

        // Abstract clock. Tests replace it with a fixed time; production uses the system clock.
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
