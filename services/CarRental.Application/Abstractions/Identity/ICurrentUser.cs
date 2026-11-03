namespace CarRental.Application.Abstractions.Identity;

/// <summary>
/// PORT: who is making this request. Implemented in the Api layer from the HTTP user (claims).
/// The Application layer never touches HttpContext, JWTs or ASP.NET Core Identity directly, so the
/// sign-in technology can change (Identity -> Entra ID / Auth0) without touching a use case.
/// </summary>
public interface ICurrentUser
{
    /// <summary>Opaque subject id of the signed-in user; null when anonymous.</summary>
    string? UserId { get; }

    string? Email { get; }

    /// <summary>The user's roles from the token (e.g. "Agent"). Empty when anonymous or a plain customer.</summary>
    IReadOnlyList<string> Roles { get; }

    bool IsInRole(string role);
}

/// <summary>Role names the Application checks itself (the API also enforces them on endpoints).</summary>
public static class StaffRoles
{
    /// <summary>Counter staff: check-outs, returns, fleet, inspections, any customer's booking.</summary>
    public const string Agent = "Agent";
}
