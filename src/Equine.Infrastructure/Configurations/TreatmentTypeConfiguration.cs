using Equine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Equine.Infrastructure.Configurations;

public class TreatmentTypeConfiguration : IEntityTypeConfiguration<TreatmentType>
{
    public void Configure(EntityTypeBuilder<TreatmentType> builder)
    {
        builder.ToTable("treatment_types");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Slug).HasMaxLength(80).IsRequired();
        builder.Property(t => t.ShortDescription).HasMaxLength(500).IsRequired();
        builder.Property(t => t.PublicDescription).HasMaxLength(2000);
        builder.Property(t => t.Colour).HasMaxLength(20);
        builder.Property(t => t.AllowedLocationTypes).HasMaxLength(500);
        builder.Property(t => t.JournalTemplateJson).HasColumnType("jsonb");
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20).HasDefaultValue(TreatmentTypeStatus.Active);
        builder.Property(t => t.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(t => t.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(t => t.TenantId);
        builder.HasIndex(t => t.Name);
        builder.HasIndex(t => new { t.TenantId, t.Slug }).IsUnique();
    }
}
