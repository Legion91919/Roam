using System.Security.Cryptography;
using CarRental.Application.Abstractions.Identity;
using CarRental.Domain.Common;
using Microsoft.AspNetCore.Identity;

namespace CarRental.Infrastructure.Identity;

/// <summary>
/// ADAPTER for IUserAccounts on ASP.NET Core Identity. Identity stores only the password HASH; the
/// clear temporary password exists in memory just long enough to be returned to the agent once.
/// </summary>
internal sealed class IdentityUserAccounts(UserManager<IdentityUser> users) : IUserAccounts
{
    public async Task<NewUserAccount> CreateUserAsync(string email, CancellationToken cancellationToken)
    {
        var user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
        var password = TemporaryPassword();
        var result = await users.CreateAsync(user, password);

        if (result.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName)))
            throw new DomainException("account.email_in_use", "An account with this email already exists.");
        if (!result.Succeeded)
            throw new InvalidOperationException("Could not create the account: " + string.Join(", ", result.Errors.Select(e => e.Code)));

        return new NewUserAccount(user.Id, password);
    }

    /// <summary>16 characters from a cryptographic RNG, with at least one of each class Identity requires.</summary>
    internal static string TemporaryPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ", lower = "abcdefghijkmnpqrstuvwxyz", digits = "23456789", symbols = "!@#$%*?";
        const string all = upper + lower + digits + symbols;

        var chars = new List<char>
        {
            upper[RandomNumberGenerator.GetInt32(upper.Length)],
            lower[RandomNumberGenerator.GetInt32(lower.Length)],
            digits[RandomNumberGenerator.GetInt32(digits.Length)],
            symbols[RandomNumberGenerator.GetInt32(symbols.Length)],
        };
        while (chars.Count < 16)
            chars.Add(all[RandomNumberGenerator.GetInt32(all.Length)]);

        // Fisher-Yates shuffle so the required classes are not always in the first four positions.
        for (var i = chars.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
        return new string(chars.ToArray());
    }
}
