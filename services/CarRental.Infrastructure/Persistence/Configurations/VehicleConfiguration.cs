using CarRental.Domain.Branches;
using CarRental.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.Infrastructure.Persistence.Configurations;

internal sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("Vehicles", table =>
        {
            table.HasCheckConstraint("CK_Vehicles_Odometer", "[OdometerKm] >= 0");
        });
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();

        // Value objects with one value: stored as a plain column, validated again when read back.
        builder.Property(v => v.Registration).HasConversion(r => r.Value, value => Registration.Create(value)).HasMaxLength(16).IsRequired();
        builder.Property(v => v.Vin).HasConversion(v => v.Value, value => Vin.Create(value)).HasMaxLength(17).IsFixedLength().IsRequired();

        // Unique across the fleet (defence in depth behind the repository checks).
        builder.HasIndex(v => v.Registration).IsUnique();
        builder.HasIndex(v => v.Vin).IsUnique();

        builder.Property(v => v.Make).HasMaxLength(40).IsRequired();
        builder.Property(v => v.Model).HasMaxLength(40).IsRequired();
        builder.Property(v => v.Category).HasConversion<string>().HasMaxLength(16);
        builder.Property(v => v.Transmission).HasConversion<string>().HasMaxLength(16);
        builder.Property(v => v.Status).HasConversion<string>().HasMaxLength(16);

        // The logbook (service visits, moves, retirement) as a JSON column: always read with the car.
        builder.OwnsMany(v => v.Log, log =>
        {
            log.ToJson("Logbook");
            log.Property(l => l.Kind).HasConversion<string>();
        });
        builder.Navigation(v => v.Log).UsePropertyAccessMode(PropertyAccessMode.Field).HasField("_log");

        builder.Property<byte[]>("RowVersion").IsRowVersion();
        builder.Ignore(v => v.DomainEvents);

        builder.HasOne<Branch>().WithMany().HasForeignKey(v => v.HomeBranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(v => v.CurrentBranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(v => new { v.CurrentBranchId, v.Category, v.Status });
    }
}
