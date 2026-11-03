using System.Security.Claims;
using CarRental.Application.Abstractions.Identity;

namespace CarRental.Api.Infrastructure;

/// <summary>
/// ADAPTER for ICurrentUser: reads the authenticated principal that ASP.NET Core built from the
/// bearer token. This is the ONLY class that knows identity arrives via HTTP claims.
/// </summary>
internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal =>
        accessor.HttpContext?.User is { Identity.IsAuthenticated: true } user ? user : null;

    public string? UserId => Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? Email => Principal?.FindFirstValue(ClaimTypes.Email) ?? Principal?.FindFirstValue(ClaimTypes.Name);

    public IReadOnlyList<string> Roles =>
        Principal?.FindAll(ClaimTypes.Role).Select(c => c.Value).Distinct().ToList() ?? (IReadOnlyList<string>)Array.Empty<string>();

    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;
}
