using CarRental.Application.Abstractions.Identity;
using CarRental.Application.Counter.AgentRegisterCustomer;
using CarRental.Application.Tests.Fakes;
using CarRental.Domain.Common;
using CarRental.Domain.Customers;
using CarRental.Domain.ValueObjects;
using static CarRental.Application.Tests.Fakes.TestDomain;

namespace CarRental.Application.Tests.Counter;

public class AgentRegisterCustomerHandlerTests
{
    private readonly InMemoryCustomerRepository _customers = new();
    private readonly FakeUserAccounts _accounts = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private AgentRegisterCustomerHandler Handler() => new(_accounts, _customers, _unitOfWork, new FixedClock(Now));

    private static AgentRegisterCustomerCommand WalkIn(string idNumber = "9005105009088", DateOnly? dob = null, string email = "walk.in@example.com") => new(
        email, "Lerato", "Dlamini", "+27 82 111 2222", IdentityDocumentType.SouthAfricanId, idNumber, null,
        dob ?? new DateOnly(1990, 5, 10), new DateOnly(2010, 6, 1));

    [Fact]
    public async Task Creates_the_account_and_the_driver_profile_and_returns_the_temporary_password_once()
    {
        var result = await Handler().Handle(WalkIn(), CancellationToken.None);

        var customer = Assert.Single(_customers.Items);
        Assert.Equal("user-for-walk.in@example.com", customer.UserId);
        Assert.Equal("walk.in@example.com", customer.Email);
        Assert.Equal(LoyaltyTier.None, customer.LoyaltyTier);
        Assert.Equal(FakeUserAccounts.Password, result.TemporaryPassword);
        Assert.Equal(1, _unitOfWork.Commits);
    }

    [Fact]
    public async Task A_date_of_birth_that_does_not_match_the_SA_ID_is_rejected_before_any_account_is_created()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() => Handler().Handle(WalkIn(dob: new DateOnly(1991, 1, 1)), CancellationToken.None));

        Assert.Equal("customer.birth_date_mismatch", ex.Code);
        Assert.Empty(_accounts.Created);
        Assert.Empty(_customers.Items);
    }

    [Fact]
    public async Task An_invalid_SA_ID_is_rejected_before_any_account_is_created()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() => Handler().Handle(WalkIn(idNumber: "9005105009089"), CancellationToken.None));

        Assert.Equal("identity.invalid_sa_id", ex.Code);
        Assert.Empty(_accounts.Created);
    }

    [Fact]
    public async Task One_identity_document_per_account()
    {
        await Handler().Handle(WalkIn(), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DomainException>(() => Handler().Handle(WalkIn(email: "someone.else@example.com"), CancellationToken.None));

        Assert.Equal("customer.document_in_use", ex.Code);
        Assert.Single(_accounts.Created);
    }

    [Fact]
    public async Task An_email_that_already_has_an_account_is_rejected()
    {
        _accounts.Taken.Add("walk.in@example.com");

        var ex = await Assert.ThrowsAsync<DomainException>(() => Handler().Handle(WalkIn(), CancellationToken.None));

        Assert.Equal("account.email_in_use", ex.Code);
        Assert.Empty(_customers.Items);
    }

    private sealed class FakeUserAccounts : IUserAccounts
    {
        public const string Password = "Temp#Pass1234abc";
        public List<string> Created { get; } = new();
        public HashSet<string> Taken { get; } = new();

        public Task<NewUserAccount> CreateUserAsync(string email, CancellationToken cancellationToken)
        {
            if (Taken.Contains(email) || Created.Contains(email))
                throw new DomainException("account.email_in_use", "An account with this email already exists.");
            Created.Add(email);
            return Task.FromResult(new NewUserAccount($"user-for-{email}", Password));
        }
    }
}
