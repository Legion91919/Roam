using CarRental.Application.Abstractions.Persistence;
using CarRental.Domain.Customers;
using CarRental.Domain.Identifiers;
using CarRental.Domain.Inspections;
using CarRental.Domain.Inventory;
using CarRental.Domain.Payments;
using CarRental.Domain.Rentals;
using CarRental.Domain.Reservations;
using CarRental.Domain.ValueObjects;
using CarRental.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Infrastructure.Persistence.Repositories;

// ADAPTERS for the write-side repository ports. Deliberately boring: load a whole aggregate
// (owned value objects come with it automatically), or add one. No SaveChanges here: committing
// is the unit of work's job, so one use case = one transaction, however many repositories it uses.

internal sealed class ReservationRepository(CarRentalDbContext db) : IReservationRepository
{
    public Task<Reservation?> GetAsync(ReservationId id, CancellationToken cancellationToken) =>
        db.Reservations.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);

    public void Add(Reservation reservation) => db.Reservations.Add(reservation);
}

internal sealed class RentalRepository(CarRentalDbContext db) : IRentalRepository
{
    public Task<Rental?> GetAsync(RentalId id, CancellationToken cancellationToken) =>
        db.Rentals.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<Rental?> GetByReservationAsync(ReservationId reservationId, CancellationToken cancellationToken) =>
        db.Rentals.SingleOrDefaultAsync(r => r.ReservationId == reservationId, cancellationToken);

    public void Add(Rental rental) => db.Rentals.Add(rental);
}

internal sealed class VehicleRepository(CarRentalDbContext db) : IVehicleRepository
{
    public Task<Vehicle?> GetAsync(VehicleId id, CancellationToken cancellationToken) =>
        db.Vehicles.SingleOrDefaultAsync(v => v.Id == id, cancellationToken);

    public Task<bool> RegistrationInUseAsync(Registration registration, CancellationToken cancellationToken) =>
        db.Vehicles.AnyAsync(v => v.Registration == registration, cancellationToken);

    public Task<bool> VinInUseAsync(Vin vin, CancellationToken cancellationToken) =>
        db.Vehicles.AnyAsync(v => v.Vin == vin, cancellationToken);

    public void Add(Vehicle vehicle) => db.Vehicles.Add(vehicle);
}

internal sealed class InspectionRepository(CarRentalDbContext db) : IInspectionRepository
{
    public Task<InspectionReport?> GetAsync(InspectionId id, CancellationToken cancellationToken) =>
        db.Inspections.SingleOrDefaultAsync(i => i.Id == id, cancellationToken);

    public Task<InspectionReport?> GetAsync(ReservationId reservationId, InspectionKind kind, CancellationToken cancellationToken) =>
        db.Inspections.SingleOrDefaultAsync(i => i.ReservationId == reservationId && i.Kind == kind, cancellationToken);

    public void Add(InspectionReport report) => db.Inspections.Add(report);
}

internal sealed class PaymentRepository(CarRentalDbContext db) : IPaymentRepository
{
    public Task<Payment?> GetByReservationAsync(ReservationId reservationId, CancellationToken cancellationToken) =>
        db.Payments.SingleOrDefaultAsync(p => p.ReservationId == reservationId, cancellationToken);

    public void Add(Payment payment) => db.Payments.Add(payment);
}

internal sealed class CategoryInventoryRepository(CarRentalDbContext db) : ICategoryInventoryRepository
{
    public async Task<IReadOnlyList<CategoryInventory>> GetRangeAsync(
        BranchId branchId, VehicleCategory category, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        await db.Inventory
            .Where(i => i.BranchId == branchId && i.Category == category && i.Date >= from && i.Date <= to)
            .OrderBy(i => i.Date)
            .ToListAsync(cancellationToken);
}

internal sealed class CustomerRepository(CarRentalDbContext db) : ICustomerRepository
{
    public Task<Customer?> GetByUserIdAsync(string userId, CancellationToken cancellationToken) =>
        db.Customers.SingleOrDefaultAsync(c => c.UserId == userId, cancellationToken);

    public Task<bool> DocumentInUseAsync(IdentityDocument document, CustomerId? exceptCustomer, CancellationToken cancellationToken)
    {
        var type = document.Type;
        var number = document.Number;
        var query = db.Customers.Where(c => c.Document.Type == type && c.Document.Number == number);
        if (exceptCustomer is { } except)
            query = query.Where(c => c.Id != except);
        return query.AnyAsync(cancellationToken);
    }

    public void Add(Customer customer) => db.Customers.Add(customer);
}

internal sealed class BranchRepository(CarRentalDbContext db) : IBranchRepository
{
    public Task<bool> ExistsAsync(BranchId id, CancellationToken cancellationToken) =>
        db.Branches.AnyAsync(b => b.Id == id, cancellationToken);
}
