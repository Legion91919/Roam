using CarRental.Application.Abstractions.Payments;
using CarRental.Application.Abstractions.Persistence;
using CarRental.Application.Abstractions.Terms;
using CarRental.Application.Common.Customers;
using CarRental.Application.Payments;
using CarRental.Application.Pricing;
using CarRental.Domain.Common;
using CarRental.Domain.Identifiers;
using CarRental.Domain.Inventory;
using CarRental.Domain.Payments;
using CarRental.Domain.Pricing;
using CarRental.Domain.Reservations;
using CarRental.Domain.Terms;
using CarRental.Domain.ValueObjects;
using MediatR;

namespace CarRental.Application.Reservations.CreateReservation;

/// <summary>
/// The CreateReservation vertical slice: command, validator and this handler sit in one folder,
/// so everything about this use case is in one place.
/// </summary>
/// <remarks>
/// A command handler is deliberately THIN. It orchestrates; the Domain decides:
///   1. Who: the signed-in user's Customer (401 if anonymous, 422 if no profile yet).
///   2. Translate primitives into value objects (RentalPeriod validates itself).
///   3. Terms: the CURRENT terms version must have been accepted (TermsAcceptance, 422 otherwise).
///   4. Price it (RentalPricer + IPriceRule strategies) using the VERIFIED driver profile.
///   5. Create the Reservation aggregate (pickup time, eligibility, raises ReservationCreated).
///   6. Reserve one car per occupied day (overbooking guard, 409 when sold out).
///   7. Card now: authorise the quoted total on the card (through the gateway port, with the reservation
///      id as idempotency key). A decline throws payment.declined (HTTP 402) and nothing is saved.
///      Pay at the branch: nothing is taken now; the agent takes the card at the counter (TakeCounterPayment).
///   8. Save once: inventory updates + reservation + payment commit in ONE transaction. If another
///      request changed one of those inventory rows first, the rowversion check fails, nothing is
///      written, and ConcurrencyRetryBehavior re-runs this handler with fresh data. The retry uses the
///      same reservation id, so the gateway returns the first authorisation instead of a second hold.
/// There are no ifs about business rules in here: if a rule is broken, the Domain throws.
///
/// SOLID (S) Single Responsibility: one handler, one use case. Cancelling is a different class.
/// SOLID (D) Dependency Inversion: every dependency is an abstraction (repository ports, unit of
///           work, terms provider, payment gateway, TimeProvider) or a pure domain service. Tests pass in-memory fakes.
/// </remarks>
public sealed class CreateReservationHandler(
    CurrentCustomer currentCustomer,
    IBranchRepository branches,
    ICategoryInventoryRepository inventory,
    IReservationRepository reservations,
    IPaymentRepository payments,
    IPaymentGateway gateway,
    IUnitOfWork unitOfWork,
    ITermsProvider terms,
    RentalPricer pricer,
    TimeProvider clock)
    : IRequestHandler<CreateReservationCommand, CreateReservationResult>
{
    public async Task<CreateReservationResult> Handle(CreateReservationCommand command, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();

        // 1. Who is booking (verified profile, not request data)
        var customer = await currentCustomer.RequireAsync(cancellationToken);
        var driver = customer.ToDriverProfile();

        // 2. Primitives -> value objects
        var period = RentalPeriod.Create(command.PickupAt, command.ReturnAt);
        var pickupBranch = new BranchId(command.PickupBranchId);
        var returnBranch = new BranchId(command.ReturnBranchId);
        if (!await branches.ExistsAsync(returnBranch, cancellationToken))
            throw new DomainException("branch.not_found", "The return branch does not exist.");

        // 3. Terms and conditions
        var acceptance = TermsAcceptance.Accept(command.AcceptedTermsVersion, terms.Current.Version, now);

        // 4. Price
        var quote = pricer.Quote(new PricingRequest(command.Category, period, driver, pickupBranch, returnBranch, command.Extras));

        // 5. Create the aggregate (throws DomainException if a rule is broken)
        var reservation = Reservation.Create(
            customer.Id, command.Category, pickupBranch, returnBranch,
            period, driver, command.Extras, quote, acceptance, now, new ReservationId(command.ReservationId), command.PaymentOption);

        // 6. Reserve one car per occupied day (throws CapacityExceededException when sold out)
        var days = await inventory.GetRangeAsync(pickupBranch, command.Category, period.FirstDay, period.LastDay, cancellationToken);
        InventoryAllocation.Reserve(pickupBranch, command.Category, period, days);

        // 7. Hold the money now, unless the customer pays at the branch
        //    (throws PaymentDeclinedException on a decline: nothing has been saved yet)
        Payment? payment = null;
        if (reservation.PaymentOption == PaymentOption.Card)
        {
            var authorization = await gateway.AuthorizeAsync(
                command.PaymentToken ?? string.Empty, quote.Total, PaymentIdempotencyKey.Authorize(reservation.Id), cancellationToken);
            payment = Payment.Authorize(reservation.Id, quote.Total,
                new CardSummary(authorization.CardBrand ?? "Card", authorization.CardLast4 ?? "0000"),
                authorization.Approved, authorization.Reference, authorization.ReasonCode, now);
            payments.Add(payment);
        }

        reservations.Add(reservation);

        // 8. One atomic commit, then domain events are published (e.g. to update the Mongo read model)
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateReservationResult(reservation.Id.Value, QuoteDto.From(quote),
            payment is null ? null : PaymentSummaryDto.From(payment), reservation.PaymentOption.ToString());
    }
}
