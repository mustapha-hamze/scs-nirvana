using Domains.Entities.ContentManagement;
using Domains.Entities.General;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.CMS;

// Mirrors the DBA-owned SQL contract for CMS_ContentTranslationJobs.
public class ContentTranslationJobConfiguration : IEntityTypeConfiguration<ContentTranslationJob>
{
    public void Configure(EntityTypeBuilder<ContentTranslationJob> builder)
    {
        builder.ToTable("CMS_ContentTranslationJobs");
        builder.ConfigureAudit();

        builder.Property(x => x.ContentId).IsRequired();
        builder.Property(x => x.CultureId).IsRequired();
        builder.Property(x => x.SourceFingerprint).IsRequired().HasMaxLength(64).IsUnicode(false);
        builder.Property(x => x.State).IsRequired();
        builder.Property(x => x.AttemptCount).IsRequired();
        builder.Property(x => x.NextAttemptAt).IsRequired();
        builder.Property(x => x.LeaseOwner).HasMaxLength(100).IsUnicode(false);
        builder.Property(x => x.ErrorCode).HasMaxLength(64).IsUnicode(false);
        builder.Property(x => x.Version).IsRequired().IsConcurrencyToken();

        builder.HasIndex(x => new { x.ContentId, x.CultureId, x.SourceFingerprint }).IsUnique();
        builder.HasIndex(x => new { x.State, x.NextAttemptAt });

        builder.HasOne<Content>().WithMany().HasForeignKey(x => x.ContentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Culture>().WithMany().HasForeignKey(x => x.CultureId).OnDelete(DeleteBehavior.Restrict);
    }
}
