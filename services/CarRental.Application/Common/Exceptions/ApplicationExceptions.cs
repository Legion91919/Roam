namespace CarRental.Application.Common.Exceptions;

/// <summary>
/// Thrown by IUnitOfWork when an optimistic concurrency token did not match: someone else changed
/// the same row between our read and our write. Declared here (not in Infrastructure) so the
/// Application layer can react to it without referencing EF Core's DbUpdateConcurrencyException.
/// The API maps it to HTTP 409 Conflict if retries are exhausted.
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>The requested aggregate does not exist. Mapped to HTTP 404.</summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string resource, object key) : base($"{resource} '{key}' was not found.")
    {
        Resource = resource;
        Key = key;
    }

    public string Resource { get; }
    public object Key { get; }
}

/// <summary>The use case needs a signed-in user and there isn't one. Mapped to HTTP 401.</summary>
public sealed class UnauthenticatedException : Exception
{
    public UnauthenticatedException() : base("You need to sign in to do that.") { }
}
