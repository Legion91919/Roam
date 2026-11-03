using CarRental.Application.Abstractions.ReadModels;
using CarRental.Application.Account.GetMyAccount;
using CarRental.Application.Common.Exceptions;
using CarRental.Application.Counter;
using CarRental.Application.Counter.FindReservationByReference;
using CarRental.Application.Counter.GetBranchReturns;
using CarRental.Application.Tests.Fakes;
using static CarRental.Application.Tests.Fakes.TestDomain;

namespace CarRental.Application.Tests.Counter;

public class CounterQueryTests
{
    private static readonly Guid Id = Guid.Parse("8f4b92a1-0c7e-449d-9a51-0b7c1d2e3f01");

    [Theory]
    [InlineData("ROAM-8F4B-92A1-0C7E-449D")]
    [InlineData("roam-8f4b-92a1-0c7e-449d")]
    [InlineData("ROAM 8F4B 92A1 0C7E 449D")]
    [InlineData("8F4B92A10C7E449D")]
    public void A_booking_reference_is_parsed_in_any_case_with_or_without_separators(string input)
    {
        Assert.True(BookingReference.TryParse(input, out var id, out var prefix));
        Assert.Equal(Guid.Empty, id);
        Assert.Equal("8F4B92A10C7E449D", prefix);
    }

    [Fact]
    public void A_full_reservation_id_is_accepted_as_a_reference()
    {
        Assert.True(BookingReference.TryParse(Id.ToString(), out var id, out _));
        Assert.Equal(Id, id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ROAM-8F4B")]
    [InlineData("ROAM-ZZZZ-92A1-0C7E-449D")]
    public void Anything_else_is_not_a_reference(string input) => Assert.False(BookingReference.TryParse(input, out _, out _));

    [Fact]
    public void The_reference_is_ROAM_plus_the_first_16_hex_digits() =>
        Assert.Equal("ROAM-8F4B-92A1-0C7E-449D", BookingReference.Format(Id));

    [Fact]
    public async Task Returns_flag_rentals_still_out_after_the_grace_period_as_late()
    {
        var due = Now.AddHours(-2);
        var readModel = new FakeCounterReadModel(Booking(due), Booking(Now.AddMinutes(-30)));

        var rows = await new GetBranchReturnsHandler(readModel, new FixedClock(Now))
            .Handle(new GetBranchReturnsQuery(Jnb.Value, DateOnly.FromDateTime(Now.Date)), CancellationToken.None);

        Assert.True(rows[0].Rental!.IsLate);    // 2 hours late
        Assert.False(rows[1].Rental!.IsLate);   // 30 minutes: still inside the 59-minute grace
    }

    [Fact]
    public async Task Find_by_reference_searches_by_prefix_and_by_id()
    {
        var readModel = new FakeCounterReadModel(Booking(Now));
        var handler = new FindReservationByReferenceHandler(readModel, new FixedClock(Now));

        Assert.NotNull(await handler.Handle(new FindReservationByReferenceQuery("ROAM-8F4B-92A1-0C7E-449D"), CancellationToken.None));
        Assert.NotNull(await handler.Handle(new FindReservationByReferenceQuery(Id.ToString()), CancellationToken.None));
        Assert.Null(await handler.Handle(new FindReservationByReferenceQuery("ROAM-0000-0000-0000-0000"), CancellationToken.None));
    }

    [Fact]
    public async Task Account_lists_the_roles_from_the_token()
    {
        var user = new FakeCurrentUser("agent-1", "agent@roam.test");
        user.RoleList.Add("Agent");

        var account = await new GetMyAccountHandler(user).Handle(new GetMyAccountQuery(), CancellationToken.None);

        Assert.Equal("agent@roam.test", account.Email);
        Assert.Equal(["Agent"], account.Roles);
    }

    [Fact]
    public async Task Account_requires_a_signed_in_user() =>
        await Assert.ThrowsAsync<UnauthenticatedException>(() =>
            new GetMyAccountHandler(new FakeCurrentUser(null, null)).Handle(new GetMyAccountQuery(), CancellationToken.None));

    private static CounterBookingDto Booking(DateTimeOffset dueBackAt) => new(
        Id, BookingReference.Format(Id), "Economy", "CheckedOut", "Thabo Nkosi", "thabo@example.com",
        Jnb.Value, "JNB", Jnb.Value, "JNB", dueBackAt.AddDays(-3), dueBackAt, 1050m, "ZAR", null,
        new CounterRentalDto(Guid.NewGuid(), "Open", "CA 1", 1000, dueBackAt.AddDays(-3), dueBackAt, IsLate: false));

    private sealed class FakeCounterReadModel(params CounterBookingDto[] rows) : ICounterReadModel
    {
        public Task<IReadOnlyList<CounterBookingDto>> ListPickupsAsync(Guid branchId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CounterBookingDto>>(rows);
        public Task<IReadOnlyList<CounterBookingDto>> ListOpenReturnsAsync(Guid branchId, DateTimeOffset dueBefore, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CounterBookingDto>>(rows);
        public Task<CounterBookingDto?> FindAsync(Guid reservationId, CancellationToken cancellationToken) =>
            Task.FromResult(rows.FirstOrDefault(r => r.ReservationId == reservationId));
        public Task<CounterBookingDto?> FindByReferencePrefixAsync(string hexPrefix, CancellationToken cancellationToken) =>
            Task.FromResult(rows.FirstOrDefault(r => r.ReservationId.ToString("N").StartsWith(hexPrefix, StringComparison.OrdinalIgnoreCase)));
    }
}
