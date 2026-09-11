using Equine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Equine.Infrastructure.Configurations;

public class VisitConfiguration : IEntityTypeConfiguration<Visit>
{
    public void Configure(EntityTypeBuilder<Visit> builder)
    {
        builder.ToTable("visits");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Source).HasConversion<string>().HasMaxLength(20);
        builder.Property(v => v.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(v => v.UpdatedAt).HasDefaultValueSql("now()");
        builder.HasOne(v => v.Location).WithMany().HasForeignKey(v => v.LocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(v => v.Lines).WithOne(l => l.Visit).HasForeignKey(l => l.VisitId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(v => new { v.PractitionerId, v.StartsAt });
        builder.HasIndex(v => v.OccupiesSlot);
    }
}
