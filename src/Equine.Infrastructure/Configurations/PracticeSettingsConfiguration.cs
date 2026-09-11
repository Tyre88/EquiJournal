using Equine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Equine.Infrastructure.Configurations;

public class PracticeSettingsConfiguration : IEntityTypeConfiguration<PracticeSettings>
{
    public void Configure(EntityTypeBuilder<PracticeSettings> builder)
    {
        builder.ToTable("practice_settings");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Clinic).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Address).HasMaxLength(400);
        builder.Property(s => s.AddressStreet).HasMaxLength(200).HasConversion(v => v ?? "", v => v ?? "");
        builder.Property(s => s.AddressPostcode).HasMaxLength(10).HasConversion(v => v ?? "", v => v ?? "");
        builder.Property(s => s.AddressCity).HasMaxLength(100).HasConversion(v => v ?? "", v => v ?? "");
        builder.Property(s => s.Phone).HasMaxLength(50);
        builder.Property(s => s.Email).HasMaxLength(255);
        builder.Property(s => s.VehicleRegistrationNumber)
            .HasMaxLength(16)
            .HasDefaultValue("")
            .HasConversion(new ValueConverter<string, string?>(
                v => string.IsNullOrEmpty(v) ? "" : v,
                v => v ?? "",
                convertsNulls: true));
        builder.Property(s => s.UpdatedAt).HasDefaultValueSql("now()");
    }
}
