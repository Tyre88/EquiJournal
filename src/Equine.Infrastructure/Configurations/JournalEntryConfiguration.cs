using Equine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Equine.Infrastructure.Configurations;

public class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        builder.ToTable("journal_entries");

        builder.HasKey(j => j.Id);

        builder.Property(j => j.OwnerSnapshot).HasColumnType("jsonb").IsRequired();
        builder.Property(j => j.Species).HasMaxLength(50).IsRequired();
        builder.Property(j => j.Sex).HasMaxLength(50);
        builder.Property(j => j.AgeOrAgeGroup).HasMaxLength(100);
        builder.Property(j => j.AnimalIdentity).HasMaxLength(100);
        builder.Property(j => j.Anamnes).IsRequired();
        builder.Property(j => j.StatusKlinisk).IsRequired();
        builder.Property(j => j.Atgarder).IsRequired();
        builder.Property(j => j.Diagnos).HasMaxLength(2000);
        builder.Property(j => j.Differentialdiagnoser).HasMaxLength(2000);
        builder.Property(j => j.PrognosOchPlan).HasMaxLength(2000);
        builder.Property(j => j.TreatmentTypeName).HasMaxLength(200);
        builder.Property(j => j.TemplateDataJson).HasColumnType("jsonb").HasDefaultValue("{}");
        builder.Property(j => j.TemplateVersion).HasDefaultValue(1);
        builder.Property(j => j.Status).HasConversion<string>().HasMaxLength(20).HasDefaultValue(JournalStatus.Draft);
        builder.Property(j => j.SignedAt);
        builder.Property(j => j.SignedBy).HasMaxLength(255);
        builder.Property(j => j.ContentHash).HasMaxLength(64);
        builder.Property(j => j.CreatedBy).HasColumnType("uuid");
        builder.Property(j => j.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(j => j.UpdatedAt).HasDefaultValueSql("now()");
        builder.Property(j => j.PerformedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(j => j.Source).HasConversion<string>().HasMaxLength(20).HasDefaultValue(JournalSource.Practice);

        builder.HasOne(j => j.Horse)
            .WithMany(h => h.JournalEntries)
            .HasForeignKey(j => j.HorseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(j => j.HorseId);
        builder.HasIndex(j => j.BookingLineId);
        builder.HasIndex(j => new { j.HorseId, j.PerformedAt });
    }
}
