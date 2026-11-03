using CarRental.Domain.Identifiers;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CarRental.Infrastructure.Persistence.Converters;

// Strongly typed ID <-> Guid. The database sees a plain uniqueidentifier column; the C# model
// sees ReservationId, BranchId... and the compiler stops you mixing them up.

public sealed class ReservationIdConverter() : ValueConverter<ReservationId, Guid>(id => id.Value, value => new ReservationId(value));
public sealed class RentalIdConverter() : ValueConverter<RentalId, Guid>(id => id.Value, value => new RentalId(value));
public sealed class CustomerIdConverter() : ValueConverter<CustomerId, Guid>(id => id.Value, value => new CustomerId(value));
public sealed class BranchIdConverter() : ValueConverter<BranchId, Guid>(id => id.Value, value => new BranchId(value));
public sealed class InventoryIdConverter() : ValueConverter<InventoryId, Guid>(id => id.Value, value => new InventoryId(value));
public sealed class PaymentIdConverter() : ValueConverter<PaymentId, Guid>(id => id.Value, value => new PaymentId(value));
public sealed class VehicleIdConverter() : ValueConverter<VehicleId, Guid>(id => id.Value, value => new VehicleId(value));
public sealed class InspectionIdConverter() : ValueConverter<InspectionId, Guid>(id => id.Value, value => new InspectionId(value));
public sealed class AttachmentIdConverter() : ValueConverter<AttachmentId, Guid>(id => id.Value, value => new AttachmentId(value));
