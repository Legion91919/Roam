using CarRental.Application.Abstractions.Identity;
using CarRental.Application.Abstractions.Messaging;
using CarRental.Application.Common.Exceptions;
using MediatR;

namespace CarRental.Application.Account.GetMyAccount;

/// <summary>
/// Who am I? Lets the client tell a customer from counter staff after sign-in. The answer comes
/// from the validated token only: the client can't claim a role, and the API re-checks the role on
/// every staff endpoint anyway ([Authorize(Roles = Agent)]).
/// </summary>
public sealed record GetMyAccountQuery : IQuery<AccountDto>;

public sealed record AccountDto(string Email, IReadOnlyList<string> Roles);

public sealed class GetMyAccountHandler(ICurrentUser user) : IRequestHandler<GetMyAccountQuery, AccountDto>
{
    public Task<AccountDto> Handle(GetMyAccountQuery query, CancellationToken cancellationToken)
    {
        if (user.UserId is null)
            throw new UnauthenticatedException();
        return Task.FromResult(new AccountDto(user.Email ?? string.Empty, user.Roles));
    }
}
