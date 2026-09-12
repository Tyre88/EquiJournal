using Equine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Equine.Infrastructure.Configurations;

public class WidgetSettingsConfiguration : IEntityTypeConfiguration<WidgetSettings>
{
    public void Configure(EntityTypeBuilder<WidgetSettings> builder)
    {
        builder.ToTable("widget_settings");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.AllowedOriginsJson).HasColumnType("jsonb").IsRequired();
        builder.Property(s => s.BookingTerms).HasMaxLength(8000);
        builder.Property(s => s.PrivacyPolicyUrl).HasMaxLength(500);
        builder.Property(s => s.PublicBaseUrl).HasMaxLength(300).IsRequired();
        builder.Property(s => s.ContactPhone).HasMaxLength(50);
        builder.Property(s => s.ContactEmail).HasMaxLength(255);
        builder.Property(s => s.UpdatedAt).HasDefaultValueSql("now()");
        builder.HasIndex(s => s.TenantId).IsUnique();
    }
}
