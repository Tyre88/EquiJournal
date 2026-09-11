using Equine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Equine.Infrastructure.Configurations;

public class AvailabilityRuleConfiguration : IEntityTypeConfiguration<AvailabilityRule>
{
    public void Configure(EntityTypeBuilder<AvailabilityRule> builder)
    {
        builder.ToTable("availability_rules");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.DayOfWeek).HasConversion<int>();
        builder.Property(r => r.StartTime).HasColumnType("time");
        builder.Property(r => r.EndTime).HasColumnType("time");
        builder.Property(r => r.EffectiveFrom).HasColumnType("date");
        builder.Property(r => r.EffectiveTo).HasColumnType("date");
        builder.Property(r => r.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(r => r.UpdatedAt).HasDefaultValueSql("now()");
        builder.HasOne(r => r.Zone).WithMany().HasForeignKey(r => r.ZoneId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(r => new { r.PractitionerId, r.DayOfWeek });
    }
}
