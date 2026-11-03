namespace CarRental.Domain.Common;

/// <summary>
/// Thrown when a business rule (invariant) would be broken, e.g. "drivers must be 21 or older".
/// </summary>
/// <remarks>
/// Each exception carries a stable, machine-readable <see cref="Code"/> ("eligibility.under_age")
/// so the API can return it in ProblemDetails and the React client can show a friendly message
/// without parsing English text. The API maps this type to HTTP 422 Unprocessable Entity.
/// </remarks>
public class DomainException : Exception
{
    public string Code { get; }

    public DomainException(string code, string message) : base(message) => Code = code;
}

/// <summary>
/// A business rule failed because a shared resource ran out (no cars left for a day).
/// It is a separate type because the right HTTP answer is 409 Conflict, not 422.
/// </summary>
public sealed class CapacityExceededException : DomainException
{
    public CapacityExceededException(string code, string message) : base(code, message) { }
}
