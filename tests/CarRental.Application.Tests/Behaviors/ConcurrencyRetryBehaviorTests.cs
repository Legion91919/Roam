using CarRental.Application.Abstractions.Messaging;
using CarRental.Application.Common.Behaviors;
using CarRental.Application.Common.Exceptions;
using CarRental.Application.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace CarRental.Application.Tests.Behaviors;

public class ConcurrencyRetryBehaviorTests
{
    private sealed record RetryableCommand : ICommand<string>, IRetryOnConcurrencyConflict;
    private sealed record PlainCommand : ICommand<string>;

    private static ConcurrencyRetryBehavior<TRequest, string> Behavior<TRequest>(FakeUnitOfWork unitOfWork) where TRequest : notnull =>
        new(unitOfWork, NullLogger<ConcurrencyRetryBehavior<TRequest, string>>.Instance);

    [Fact]
    public async Task Retries_with_fresh_state_until_the_command_wins()
    {
        var unitOfWork = new FakeUnitOfWork();
        var calls = 0;

        var result = await Behavior<RetryableCommand>(unitOfWork).Handle(new RetryableCommand(), () =>
        {
            calls++;
            if (calls < 3) throw new ConcurrencyConflictException("lost the race");
            return Task.FromResult("booked");
        }, CancellationToken.None);

        Assert.Equal("booked", result);
        Assert.Equal(3, calls);
        Assert.Equal(2, unitOfWork.Discards);   // change tracker cleared before each retry
    }

    [Fact]
    public async Task Gives_up_after_max_attempts_so_the_api_can_return_409()
    {
        var calls = 0;

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => Behavior<RetryableCommand>(new FakeUnitOfWork())
            .Handle(new RetryableCommand(), () => { calls++; throw new ConcurrencyConflictException("always loses"); }, CancellationToken.None));

        Assert.Equal(ConcurrencyRetryBehavior<RetryableCommand, string>.MaxAttempts, calls);
    }

    [Fact]
    public async Task Commands_that_did_not_opt_in_are_never_retried()
    {
        var calls = 0;

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => Behavior<PlainCommand>(new FakeUnitOfWork())
            .Handle(new PlainCommand(), () => { calls++; throw new ConcurrencyConflictException("x"); }, CancellationToken.None));

        Assert.Equal(1, calls);
    }
}
