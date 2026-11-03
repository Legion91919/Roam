using CarRental.Domain.Branches;
using CarRental.Domain.Rentals;
using CarRental.Domain.Vehicles;
using CarRental.Domain.Reservations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.Infrastructure.Persistence.Configurations;

internal sealed class RentalConfiguration : IEntityTypeConfiguration<Rental>
{
    public void Configure(EntityTypeBuilder<Rental> builder)
    {
        builder.ToTable("Rentals", table =>
        {
            // Defence in depth: the Domain already refuses a decreasing odometer; the database refuses it too,
            // so a bad script or a future bug cannot write impossible data.
            table.HasCheckConstraint("CK_Rentals_Odometer", "[EndOdometerKm] IS NULL OR [EndOdometerKm] >= [StartOdometerKm]");
        });

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Category).HasConversion<string>().HasMaxLength(16);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(r => r.VehicleRegistration).HasMaxLength(16).IsRequired();

        builder.OwnsOne(r => r.DailyRate, money => MoneyMapping.Map(money, "DailyRate"));
        builder.Navigation(r => r.DailyRate).IsRequired();
        builder.OwnsOne(r => r.LateFee, money => MoneyMapping.Map(money, "LateFee"));
        builder.Navigation(r => r.LateFee).IsRequired();

        builder.Property<byte[]>("RowVersion").IsRowVersion();
        builder.Ignore(r => r.DomainEvents);
        builder.Ignore(r => r.DistanceKm);

        // One rental per reservation, enforced by a unique index (the database is the last line of defence).
        builder.HasOne<Reservation>().WithMany().HasForeignKey(r => r.ReservationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => r.ReservationId).IsUnique();

        // The fleet car on this rental (history timeline reads rentals by vehicle).
        builder.HasOne<Vehicle>().WithMany().HasForeignKey(r => r.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => r.VehicleId);
        builder.HasOne<Branch>().WithMany().HasForeignKey(r => r.ReturnBranchId).OnDelete(DeleteBehavior.Restrict);
    }
}
