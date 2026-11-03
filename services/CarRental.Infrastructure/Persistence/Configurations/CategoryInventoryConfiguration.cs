using CarRental.Domain.Branches;
using CarRental.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.Infrastructure.Persistence.Configurations;

/// <summary>
/// The overbooking guard at the database level. Three layers of protection:
///  1. Domain: CategoryInventory.Reserve() refuses to go past Capacity (in memory).
///  2. ROWVERSION concurrency token: two concurrent updates of the same row cannot both commit.
///  3. CHECK constraint: even raw SQL cannot make Reserved exceed Capacity or go negative.
/// Plus a UNIQUE index so there can never be two rows for the same branch/category/day.
/// </summary>
internal sealed class CategoryInventoryConfiguration : IEntityTypeConfiguration<CategoryInventory>
{
    public void Configure(EntityTypeBuilder<CategoryInventory> builder)
    {
        builder.ToTable("CategoryInventory", table =>
            table.HasCheckConstraint("CK_CategoryInventory_Reserved", "[Reserved] >= 0 AND [Reserved] <= [Capacity]"));

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.Category).HasConversion<string>().HasMaxLength(16);
        builder.Property(i => i.Date).HasColumnType("date");

        builder.Property<byte[]>("RowVersion").IsRowVersion();   // the concurrency token
        builder.Ignore(i => i.Available);
        builder.Ignore(i => i.DomainEvents);

        builder.HasIndex(i => new { i.BranchId, i.Category, i.Date }).IsUnique();
        builder.HasOne<Branch>().WithMany().HasForeignKey(i => i.BranchId).OnDelete(DeleteBehavior.Restrict);
    }
}
