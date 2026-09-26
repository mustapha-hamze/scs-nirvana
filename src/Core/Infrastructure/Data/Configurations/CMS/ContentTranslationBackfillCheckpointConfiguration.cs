using Domains.Entities.ContentManagement;
using Domains.Entities.General;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.CMS;

// Mirrors the DBA-owned SQL contract for CMS_ContentTranslationBackfillCheckpoints.
public class ContentTranslationBackfillCheckpointConfiguration : IEntityTypeConfiguration<ContentTranslationBackfillCheckpoint>
{
    public void Configure(EntityTypeBuilder<ContentTranslationBackfillCheckpoint> builder)
    {
        builder.ToTable("CMS_ContentTranslationBackfillCheckpoints");
        builder.ConfigureAudit();

        builder.Property(x => x.RunKey).IsRequired().HasMaxLength(100).IsUnicode(false);
        builder.Property(x => x.CultureId).IsRequired();
        builder.Property(x => x.LastContentId).IsRequired();
        builder.Property(x => x.ScannedCount).IsRequired();
        builder.Property(x => x.ImportedStaleCount).IsRequired();
        builder.Property(x => x.NeedsReviewCount).IsRequired();
        builder.Property(x => x.SkippedExistingCount).IsRequired();
        builder.Property(x => x.Version).IsRequired().IsConcurrencyToken();

        builder.HasIndex(x => x.RunKey).IsUnique();

        builder.HasOne<Culture>().WithMany().HasForeignKey(x => x.CultureId).OnDelete(DeleteBehavior.Restrict);
    }
}
