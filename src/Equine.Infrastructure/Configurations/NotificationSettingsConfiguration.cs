using Equine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Equine.Infrastructure.Configurations;

public class NotificationSettingsConfiguration : IEntityTypeConfiguration<NotificationSettings>
{
    public void Configure(EntityTypeBuilder<NotificationSettings> builder)
    {
        builder.ToTable("notification_settings");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.EnabledJson).HasColumnType("jsonb").IsRequired();
        builder.Property(s => s.UpdatedAt).HasDefaultValueSql("now()");
        builder.HasIndex(s => s.TenantId).IsUnique();
    }
}
