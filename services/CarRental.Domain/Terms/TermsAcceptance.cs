using CarRental.Domain.Common;

namespace CarRental.Domain.Terms;

/// <summary>
/// VALUE OBJECT: proof that the customer accepted a specific version of the rental terms, and when.
/// Stored on the reservation, so we can always show which terms applied to a booking even after
/// the terms change.
/// </summary>
public sealed record TermsAcceptance
{
    public string Version { get; private init; }
    public DateTimeOffset AcceptedAt { get; private init; }

    private TermsAcceptance(string version, DateTimeOffset acceptedAt)
    {
        Version = version;
        AcceptedAt = acceptedAt;
    }

    /// <summary>
    /// Business rule: a booking requires acceptance of the CURRENT terms. Accepting an old version
    /// (e.g. a browser tab left open across a terms update) is rejected so the customer re-reads them.
    /// </summary>
    public static TermsAcceptance Accept(string? acceptedVersion, string currentVersion, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(acceptedVersion))
            throw new DomainException("terms.not_accepted", "You must accept the rental terms and conditions to book.");
        if (!string.Equals(acceptedVersion.Trim(), currentVersion, StringComparison.Ordinal))
            throw new DomainException("terms.outdated", "The rental terms have changed. Please review and accept the current version.");

        return new TermsAcceptance(currentVersion, now);
    }
}
