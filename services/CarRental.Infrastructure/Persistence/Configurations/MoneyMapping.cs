using CarRental.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.Infrastructure.Persistence.Configurations;

/// <summary>
/// One mapping for Money, reused by every owner, so every money column in the database has the same
/// shape: {Prefix}Amount decimal(18,2) + {Prefix}Currency char(3).
/// </summary>
/// <remarks>
/// OWNED TYPE: Money has no identity and no table of its own. Its columns live on the owner's row
/// and it is always loaded with the owner. (EF Core 8 also offers ComplexProperty for value objects;
/// OwnsOne is used here because it is the long-established, widely known option.)
/// </remarks>
internal static class MoneyMapping
{
    public static void Map<TOwner>(OwnedNavigationBuilder<TOwner, Money> money, string prefix)
        where TOwner : class
    {
        money.Property(m => m.Amount).HasColumnName($"{prefix}Amount").HasPrecision(18, 2).IsRequired();
        money.Property(m => m.Currency).HasColumnName($"{prefix}Currency").HasMaxLength(3).IsFixedLength().IsRequired();
    }
}
