using Equine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Equine.Infrastructure.Configurations;

public class ZoneConfiguration : IEntityTypeConfiguration<Zone>
{
    public void Configure(EntityTypeBuilder<Zone> builder)
    {
        builder.ToTable("zones");
        builder.HasKey(z => z.Id);
        builder.Property(z => z.Name).HasMaxLength(200).IsRequired();
        builder.Property(z => z.PostcodesJson).HasColumnType("jsonb").IsRequired();
        builder.Property(z => z.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(z => z.UpdatedAt).HasDefaultValueSql("now()");
        builder.HasIndex(z => z.Name).IsUnique();
        builder.HasIndex(z => z.IsFallback).IsUnique().HasFilter("\"IsFallback\" = TRUE");
    }
}
