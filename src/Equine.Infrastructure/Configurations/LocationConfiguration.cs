using Equine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Equine.Infrastructure.Configurations;

public class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.ToTable("locations");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Type).HasConversion<string>().HasMaxLength(30);
        builder.Property(l => l.Name).HasMaxLength(200).IsRequired();
        builder.Property(l => l.AddressStreet).HasMaxLength(200);
        builder.Property(l => l.AddressPostcode).HasMaxLength(10);
        builder.Property(l => l.AddressCity).HasMaxLength(100);
        builder.Property(l => l.NormalizedKey).HasMaxLength(400).IsRequired();
        builder.Property(l => l.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(l => l.UpdatedAt).HasDefaultValueSql("now()");
        builder.HasIndex(l => l.NormalizedKey);
    }
}
