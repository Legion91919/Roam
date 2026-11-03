using FluentValidation;

namespace CarRental.Application.Reservations.CreateReservation;

/// <summary>
/// Shape validation only (HTTP 400). Business rules such as minimum age or accepting the terms live
/// in the Domain (HTTP 422).
/// </summary>
public sealed class CreateReservationValidator : AbstractValidator<CreateReservationCommand>
{
    public CreateReservationValidator()
    {
        RuleFor(c => c.PickupBranchId).NotEmpty();
        RuleFor(c => c.ReturnBranchId).NotEmpty();
        RuleFor(c => c.Category).IsInEnum();
        RuleFor(c => c.ReturnAt).GreaterThan(c => c.PickupAt).WithMessage("Return time must be after pickup time.");
        RuleFor(c => c.PaymentOption).IsInEnum();
        // Card now: a token from the gateway's hosted fields. Pay at the branch: no card data at all.
        RuleFor(c => c.PaymentToken).NotEmpty().WithMessage("A payment card is required to book, or choose to pay at the branch.").MaximumLength(100)
            .When(c => c.PaymentOption == Domain.Reservations.PaymentOption.Card);
        RuleFor(c => c.PaymentToken).Empty().WithMessage("Paying at the branch: do not send a card.")
            .When(c => c.PaymentOption == Domain.Reservations.PaymentOption.PayAtBranch);
        RuleFor(c => c.ReservationId).NotEmpty();
    }
}
