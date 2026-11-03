namespace CarRental.Api.Infrastructure;

/// <summary>
/// DEVELOPMENT DEMO AID: the system clock shifted by a fixed offset (config DemoClock:Offset, e.g.
/// "12.02:00:00"), so a reviewer can check a car out today and then restart the API "12 days and
/// 2 hours later" to see a late return. Only wired up when the environment is Development.
/// </summary>
internal sealed class OffsetTimeProvider(TimeSpan offset) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => System.GetUtcNow() + offset;
}
