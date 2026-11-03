using CarRental.Domain.Branches;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.Infrastructure.Persistence.Configurations;

internal sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("Branches");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();
        builder.Property(b => b.Code).HasMaxLength(8).IsRequired();
        builder.Property(b => b.Name).HasMaxLength(100).IsRequired();
        builder.Property(b => b.City).HasMaxLength(60).IsRequired();
        builder.Property(b => b.Type).HasConversion<string>().HasMaxLength(16);
        builder.Property(b => b.Address).HasMaxLength(200);
        builder.HasIndex(b => b.Code).IsUnique();
        builder.Ignore(b => b.DomainEvents);
    }
}
