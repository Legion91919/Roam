using CarRental.Domain.Branches;
using CarRental.Domain.Customers;
using CarRental.Domain.Reservations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.Infrastructure.Persistence.Configurations;

internal sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.ToTable("Reservations");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();   // IDs are created in the Domain, not by the database

        // Enums as readable strings: survives reordering the enum and is readable in SQL.
        builder.Property(r => r.Category).HasConversion<string>().HasMaxLength(16);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(16);
        // Card | PayAtBranch. (A production migration adds the column with default 'Card': every
        // existing booking was paid by card online.)
        builder.Property(r => r.PaymentOption).HasConversion<string>().HasMaxLength(16);
        builder.Property(r => r.Extras);   // [Flags] enum stays an int (combinations)

        // ---- Value objects as OWNED TYPES (columns on the Reservations row) ----
        builder.OwnsOne(r => r.Period, period =>
        {
            period.Property(p => p.PickupAt).HasColumnName("PickupAt").IsRequired();
            period.Property(p => p.ReturnAt).HasColumnName("ReturnAt").IsRequired();
        });
        builder.Navigation(r => r.Period).IsRequired();

        builder.OwnsOne(r => r.Driver, driver =>
        {
            driver.Property(d => d.DateOfBirth).HasColumnName("DriverDateOfBirth").IsRequired();
            driver.Property(d => d.LicenceIssuedOn).HasColumnName("DriverLicenceIssuedOn").IsRequired();
            driver.Property(d => d.Tier).HasColumnName("DriverLoyaltyTier").HasConversion<string>().HasMaxLength(16);
        });
        builder.Navigation(r => r.Driver).IsRequired();

        builder.OwnsOne(r => r.QuotedTotal, money => MoneyMapping.Map(money, "QuotedTotal"));
        builder.Navigation(r => r.QuotedTotal).IsRequired();

        // Which terms version the customer accepted, and when (evidence for disputes).
        builder.OwnsOne(r => r.Terms, terms =>
        {
            terms.Property(t => t.Version).HasColumnName("TermsVersion").HasMaxLength(20).IsRequired();
            terms.Property(t => t.AcceptedAt).HasColumnName("TermsAcceptedAt").IsRequired();
        });
        builder.Navigation(r => r.Terms).IsRequired();

        // Who cancelled (or recorded the no-show) and why: evidence, kept on the booking.
        builder.OwnsOne(r => r.Cancellation, cancellation =>
        {
            cancellation.Property(c => c.By).HasColumnName("CancelledBy").HasConversion<string>().HasMaxLength(16);
            cancellation.Property(c => c.ByUserId).HasColumnName("CancelledByUserId").HasMaxLength(450);
            cancellation.Property(c => c.Reason).HasColumnName("CancellationReason").HasMaxLength(200);
            cancellation.Property(c => c.At).HasColumnName("CancellationAt");
        });

        // ---- Optimistic concurrency ----
        // SHADOW property: exists in the EF model and the table, NOT on the domain class.
        // SQL Server bumps a rowversion on every update; EF adds "WHERE RowVersion = @original" to
        // UPDATEs, so two people cancelling / checking out the same reservation cannot both win.
        builder.Property<byte[]>("RowVersion").IsRowVersion();

        builder.Ignore(r => r.DomainEvents);

        // Referential integrity to customers and branches, without navigation properties in the Domain.
        builder.HasOne<Customer>().WithMany().HasForeignKey(r => r.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(r => r.PickupBranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(r => r.ReturnBranchId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.CustomerId);
        builder.HasIndex(r => new { r.PickupBranchId, r.Status });
    }
}
