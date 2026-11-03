# SOLID in this codebase

Each of the five SOLID principles is **in the code**, not just described. Every place is tagged with a
`// SOLID (X):` comment, so you can find them all with one search:

```bash
git grep -n "SOLID ("
```

The table below links each principle to the code that shows it and the test that **proves** it.

| Principle | Where to look | What to notice | Proven by |
|---|---|---|---|
| **S**: Single Responsibility | [`CreateReservationHandler`](../services/CarRental.Application/Reservations/CreateReservation/CreateReservationHandler.cs), [`CancelReservation`](../services/CarRental.Application/Reservations/CancelReservation/CancelReservation.cs) | One handler per use case. The handler only orchestrates; rules live in the Domain. | [`Every_command_and_query_has_exactly_one_handler`](../tests/CarRental.ArchitectureTests/DependencyRuleTests.cs) |
| | [`Rules.cs`](../services/CarRental.Domain/Pricing/Rules.cs) | Each pricing rule is its own class with one reason to change. | [`RentalPricerTests`](../tests/CarRental.Domain.Tests/Pricing/RentalPricerTests.cs) |
| | [`DriverEligibilityPolicy`](../services/CarRental.Domain/Eligibility/DriverEligibilityPolicy.cs) | Eligibility only, not pricing or booking. | [`DriverEligibilityPolicyTests`](../tests/CarRental.Domain.Tests/Eligibility/DriverEligibilityPolicyTests.cs) |
| | [`LoggingBehavior`](../services/CarRental.Application/Common/Behaviors/LoggingBehavior.cs), [`ValidationBehavior`](../services/CarRental.Application/Common/Behaviors/ValidationBehavior.cs) | Cross-cutting concerns are pipeline decorators, not copy-pasted into handlers. | [`PipelineWiringTests`](../tests/CarRental.Application.Tests/Behaviors/PipelineWiringTests.cs) |
| | [`ReservationsController`](../services/CarRental.Api/Controllers/ReservationsController.cs) | The controller only does HTTP: no logic, no DbContext. | |
| **O**: Open/Closed | [`IPriceRule`](../services/CarRental.Domain/Pricing/IPriceRule.cs) + [`RentalPricer`](../services/CarRental.Domain/Pricing/RentalPricer.cs) | A new promotion = a new class + one DI line. `RentalPricer` is never edited. | [`A_new_rule_can_be_added_without_modifying_the_pricer`](../tests/CarRental.Domain.Tests/Pricing/RentalPricerTests.cs) |
| | [`Application/DependencyInjection.cs`](../services/CarRental.Application/DependencyInjection.cs) | Rules are registered as `IEnumerable<IPriceRule>`. | [`All_pricing_rules_are_registered`](../tests/CarRental.Application.Tests/Behaviors/PipelineWiringTests.cs) |
| | [`ApiExceptionHandler`](../services/CarRental.Api/Infrastructure/ApiExceptionHandler.cs) | A new exception type is mapped once here, not in every controller. | |
| **L**: Liskov Substitution | [`IPriceRule`](../services/CarRental.Domain/Pricing/IPriceRule.cs) + [`PriceContext`](../services/CarRental.Domain/Pricing/PriceContext.cs) | Every rule honours one contract (never throws, only discounts the base, only adds fees, explains every change), so the pricer treats them all the same. | [`PriceRuleContractTests`](../tests/CarRental.Domain.Tests/Pricing/PriceRuleContractTests.cs) runs the same contract against **every** rule |
| | [`IAvailabilityReadModel`](../services/CarRental.Application/Abstractions/ReadModels/ReadModels.cs): [`SqlAvailabilityReadModel`](../services/CarRental.Infrastructure/ReadModels/Sql/SqlReadModels.cs) and [`MongoAvailabilityReadModel`](../services/CarRental.Infrastructure/ReadModels/Mongo/MongoAvailability.cs) | Two interchangeable implementations, chosen by config (`ReadModels:Provider`). The query handler cannot tell which one it got. | |
| **I**: Interface Segregation | [`Repositories.cs`](../services/CarRental.Application/Abstractions/Persistence/Repositories.cs) | One small repository per aggregate. No generic `IRepository<T>` with 15 methods. Saving is a separate `IUnitOfWork`. Reads have their own ports. | |
| | [`IPriceRule`](../services/CarRental.Domain/Pricing/IPriceRule.cs) | A rule implements one method and one property. | |
| **D**: Dependency Inversion | [`Abstractions/`](../services/CarRental.Application/Abstractions) (ports owned by Application) and [`Infrastructure/`](../services/CarRental.Infrastructure) (adapters) | Application defines `IReservationRepository`; Infrastructure implements it. The arrow points **inwards**. | [`DependencyRuleTests`](../tests/CarRental.ArchitectureTests/DependencyRuleTests.cs) fail if Application ever references EF Core, Mongo or ASP.NET |
| | [`CreateReservationHandler`](../services/CarRental.Application/Reservations/CreateReservation/CreateReservationHandler.cs) | Depends on ports, `RentalPricer` and `TimeProvider`, never on `DbContext` or `DateTime.Now`. | Handler tests run with in-memory [fakes](../tests/CarRental.Application.Tests/Fakes/Fakes.cs) |
| | [`RentalPricer`](../services/CarRental.Domain/Pricing/RentalPricer.cs) / [`RateCard`](../services/CarRental.Domain/Pricing/RateCard.cs) | Rules and rates are injected. | [`Rates_are_injected_so_a_different_rate_card_needs_no_code_change`](../tests/CarRental.Domain.Tests/Pricing/RentalPricerTests.cs) |

## One-line summaries

- **S**: "Each class has one reason to change: one handler per use case, one class per pricing rule."
- **O**: "To add a Black Friday discount I add a class implementing `IPriceRule` and register it. I don't edit the pricer. There's a test that proves it."
- **L**: "Any `IPriceRule` can replace any other. A contract test runs the same checks against every rule, so a new rule that breaks the rules fails the build."
- **I**: "Small, focused ports: a repository per aggregate, a separate unit of work, separate read models. Nobody depends on methods they don't use."
- **D**: "The Application layer owns the interfaces; Infrastructure implements them. An architecture test fails if anyone points a dependency the wrong way."
