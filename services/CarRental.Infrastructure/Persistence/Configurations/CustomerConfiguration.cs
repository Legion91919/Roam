using CarRental.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.Infrastructure.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        // One customer profile per signed-in user.
        builder.Property(c => c.UserId).HasMaxLength(450).IsRequired();
        builder.HasIndex(c => c.UserId).IsUnique();

        builder.Property(c => c.FirstName).HasMaxLength(60).IsRequired();
        builder.Property(c => c.LastName).HasMaxLength(60).IsRequired();
        builder.Property(c => c.Email).HasMaxLength(256).IsRequired();
        builder.Property(c => c.PhoneNumber).HasMaxLength(20).IsRequired();
        builder.Property(c => c.LoyaltyTier).HasConversion<string>().HasMaxLength(16);

        // Identity document as an OWNED TYPE, with a unique index so one ID/passport = one account.
        // (POPIA: production would encrypt DocumentNumber at rest, e.g. Always Encrypted.)
        builder.OwnsOne(c => c.Document, document =>
        {
            document.Property(d => d.Type).HasColumnName("DocumentType").HasConversion<string>().HasMaxLength(20);
            document.Property(d => d.Number).HasColumnName("DocumentNumber").HasMaxLength(20).IsRequired();
            document.Property(d => d.IssuingCountry).HasColumnName("DocumentCountry").HasMaxLength(3).IsFixedLength();
            document.HasIndex(d => new { d.Type, d.Number }).IsUnique();
        });
        builder.Navigation(c => c.Document).IsRequired();

        builder.Property<byte[]>("RowVersion").IsRowVersion();
        builder.Ignore(c => c.FullName);
        builder.Ignore(c => c.DomainEvents);
    }
}
