using CarRental.Domain.Branches;
using CarRental.Domain.Customers;
using CarRental.Domain.Identifiers;
using CarRental.Domain.Inspections;
using CarRental.Domain.Inventory;
using CarRental.Domain.Payments;
using CarRental.Domain.Rentals;
using CarRental.Domain.Reservations;
using CarRental.Domain.Vehicles;
using CarRental.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Infrastructure.Persistence;

/// <summary>
/// The EF Core unit of work + identity map for the WRITE model.
/// </summary>
/// <remarks>
/// Mapping lives in IEntityTypeConfiguration classes (Configurations/), not as attributes on the
/// domain classes: the Domain stays free of [Key], [Column] and every other persistence detail.
/// </remarks>
public sealed class CarRentalDbContext(DbContextOptions<CarRentalDbContext> options) : DbContext(options)
{
    public const string Schema = "rental";

    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<Rental> Rentals => Set<Rental>();
    public DbSet<CategoryInventory> Inventory => Set<CategoryInventory>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<InspectionReport> Inspections => Set<InspectionReport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CarRentalDbContext).Assembly);
    }

    /// <summary>
    /// Model-wide conventions: every property of a strongly typed ID type is stored as a
    /// uniqueidentifier, everywhere, without repeating HasConversion on each property.
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<ReservationId>().HaveConversion<ReservationIdConverter>();
        configurationBuilder.Properties<RentalId>().HaveConversion<RentalIdConverter>();
        configurationBuilder.Properties<CustomerId>().HaveConversion<CustomerIdConverter>();
        configurationBuilder.Properties<BranchId>().HaveConversion<BranchIdConverter>();
        configurationBuilder.Properties<InventoryId>().HaveConversion<InventoryIdConverter>();
        configurationBuilder.Properties<PaymentId>().HaveConversion<PaymentIdConverter>();
        configurationBuilder.Properties<VehicleId>().HaveConversion<VehicleIdConverter>();
        configurationBuilder.Properties<InspectionId>().HaveConversion<InspectionIdConverter>();
        configurationBuilder.Properties<AttachmentId>().HaveConversion<AttachmentIdConverter>();

        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }
}
