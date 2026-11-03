using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Infrastructure.Identity;

/// <summary>
/// ASP.NET Core Identity's tables (users, password hashes, roles) in their own "auth" schema and
/// their own DbContext. Sign-in is an Infrastructure DETAIL: the Domain only stores the user's
/// opaque id on Customer, and the Application only sees ICurrentUser. Swapping to Entra ID or
/// Auth0 means deleting this folder and changing DI, not touching a single use case.
/// </summary>
public sealed class AuthDbContext(DbContextOptions<AuthDbContext> options) : IdentityDbContext<IdentityUser>(options)
{
    public const string Schema = "auth";

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema(Schema);
    }
}

public static class Roles
{
    /// <summary>Counter staff: may check cars out and take returns.</summary>
    public const string Agent = CarRental.Application.Abstractions.Identity.StaffRoles.Agent;
}
