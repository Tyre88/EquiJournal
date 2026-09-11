using Equine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Equine.Infrastructure.Configurations;

public class BookingLineConfiguration : IEntityTypeConfiguration<BookingLine>
{
    public void Configure(EntityTypeBuilder<BookingLine> builder)
    {
        builder.ToTable("booking_lines");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.TreatmentName).HasMaxLength(200).IsRequired();
        builder.Property(b => b.Price).HasColumnType("numeric(10,2)");
        builder.Property(b => b.VatRate).HasColumnType("numeric(5,4)");
        builder.Property(b => b.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(b => b.Source).HasConversion<string>().HasMaxLength(20);
        builder.Property(b => b.ClientNote).HasMaxLength(2000);
        builder.Property(b => b.InternalNote).HasMaxLength(2000);
        builder.Property(b => b.CancellationReason).HasMaxLength(500);
        builder.Property(b => b.PublicReference).HasMaxLength(20);
        builder.Property(b => b.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(b => b.UpdatedAt).HasDefaultValueSql("now()");
        builder.HasOne(b => b.Horse).WithMany().HasForeignKey(b => b.HorseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(b => b.Owner).WithMany().HasForeignKey(b => b.OwnerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(b => b.TreatmentType).WithMany().HasForeignKey(b => b.TreatmentTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(b => b.VisitId);
        builder.HasIndex(b => b.HorseId);
        builder.HasIndex(b => b.OwnerId);
        builder.HasIndex(b => b.Status);
        builder.HasIndex(b => b.PublicReference)
            .IsUnique()
            .HasFilter("\"PublicReference\" IS NOT NULL");
        builder.HasIndex(b => new { b.Source, b.Status, b.EmailVerifiedAt });
    }
}
