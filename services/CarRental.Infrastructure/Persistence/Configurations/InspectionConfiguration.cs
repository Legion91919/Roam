using CarRental.Domain.Inspections;
using CarRental.Domain.Reservations;
using CarRental.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.Infrastructure.Persistence.Configurations;

/// <summary>
/// Inspection reports. The checklist, damage marks, new damage and attachment METADATA are JSON
/// columns: they are always read and written with their report and never queried on their own.
/// The files themselves are in IFileStorage, never in the database.
/// </summary>
internal sealed class InspectionConfiguration : IEntityTypeConfiguration<InspectionReport>
{
    public void Configure(EntityTypeBuilder<InspectionReport> builder)
    {
        builder.ToTable("Inspections");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.Kind).HasConversion<string>().HasMaxLength(16);
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(i => i.InspectedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(i => i.InspectedByName).HasMaxLength(60).IsRequired();

        builder.OwnsOne(i => i.Fuel, fuel => fuel.Property(f => f.Eighths).HasColumnName("FuelEighths"));

        builder.OwnsMany(i => i.Checklist, items =>
        {
            items.ToJson("Checklist");
            items.Property(c => c.Code).HasConversion<string>();
            items.Property(c => c.Status).HasConversion<string>();
        });
        builder.OwnsMany(i => i.Damage, damage =>
        {
            damage.ToJson("Damage");
            damage.Property(d => d.Severity).HasConversion<string>();
        });
        builder.OwnsMany(i => i.NewDamage, damage =>
        {
            damage.ToJson("NewDamage");
            damage.Property(d => d.Item).HasConversion<string>();
            damage.Property(d => d.Severity).HasConversion<string>();
        });
        builder.OwnsMany(i => i.Attachments, files =>
        {
            // EF Core 8 rejects an explicit key on a JSON collection element; a property called "Id"
            // would be picked up as one by convention, hence InspectionAttachment.AttachmentId.
            files.ToJson("Attachments");
            files.Property(a => a.Angle).HasConversion<string>();
        });
        foreach (var collection in new[] { "Checklist", "Damage", "NewDamage", "Attachments" })
            builder.Navigation(collection).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property<byte[]>("RowVersion").IsRowVersion();
        builder.Ignore(i => i.DomainEvents);
        builder.Ignore(i => i.CustomerAcknowledged);

        // One Checkout and one Return report per booking.
        builder.HasIndex(i => new { i.ReservationId, i.Kind }).IsUnique();
        builder.HasOne<Reservation>().WithMany().HasForeignKey(i => i.ReservationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Vehicle>().WithMany().HasForeignKey(i => i.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(i => i.VehicleId);
    }
}
