using CarRental.Domain.Payments;
using CarRental.Domain.Reservations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.Infrastructure.Persistence.Configurations;

/// <summary>
/// Payments table. Note what is NOT here: no card number, expiry or CVC columns. Only the gateway's
/// reference and the card's brand + last four digits (PCI DSS: card data stays with the gateway).
/// </summary>
internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments", table =>
        {
            // Defence in depth for the Domain's rules: positive hold, capture never negative.
            table.HasCheckConstraint("CK_Payments_Authorized", "[AuthorizedAmount] > 0");
            table.HasCheckConstraint("CK_Payments_Captured", "[CapturedAmount] >= 0");
        });

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(p => p.GatewayReference).HasMaxLength(100).IsRequired();
        builder.Property(p => p.FailureReason).HasMaxLength(100);

        builder.OwnsOne(p => p.Authorized, money => MoneyMapping.Map(money, "Authorized"));
        builder.Navigation(p => p.Authorized).IsRequired();
        builder.OwnsOne(p => p.Captured, money => MoneyMapping.Map(money, "Captured"));
        builder.Navigation(p => p.Captured).IsRequired();

        builder.OwnsOne(p => p.Card, card =>
        {
            card.Property(c => c.Brand).HasColumnName("CardBrand").HasMaxLength(20).IsRequired();
            card.Property(c => c.Last4).HasColumnName("CardLast4").HasMaxLength(4).IsFixedLength().IsRequired();
        });
        builder.Navigation(p => p.Card).IsRequired();

        // Capture (agent at return) and void (customer cancelling) could race: the second writer loses.
        builder.Property<byte[]>("RowVersion").IsRowVersion();
        builder.Ignore(p => p.DomainEvents);
        builder.Ignore(p => p.CaptureLimit);

        // One payment per reservation.
        builder.HasOne<Reservation>().WithMany().HasForeignKey(p => p.ReservationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => p.ReservationId).IsUnique();
    }
}
