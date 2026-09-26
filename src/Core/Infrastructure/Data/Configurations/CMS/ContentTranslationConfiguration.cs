using Domains.Entities.ContentManagement;
using Domains.Entities.General;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.CMS;

// Column names/types/lengths mirror the DBA-owned SQL contract for CMS_ContentTranslations.
public class ContentTranslationConfiguration : IEntityTypeConfiguration<ContentTranslation>
{
    public void Configure(EntityTypeBuilder<ContentTranslation> builder)
    {
        builder.ToTable("CMS_ContentTranslations");
        builder.ConfigureAudit();

        builder.Property(x => x.ContentId).IsRequired();
        builder.Property(x => x.CultureId).IsRequired();
        // SQL Server store types come from the CLR mappings: byte enum -> tinyint, DateTime? ->
        // datetime2, non-unicode length N -> varchar(N), unbounded string -> nvarchar(max).
        // Asserted against the SQL Server provider in ContentTranslationModelTests.
        builder.Property(x => x.TranslationStatus).IsRequired();
        builder.Property(x => x.SourceFingerprint).IsRequired().HasMaxLength(64).IsUnicode(false);
        builder.Property(x => x.Provider).HasMaxLength(128).IsUnicode(false);
        builder.Property(x => x.Model).HasMaxLength(128).IsUnicode(false);
        builder.Property(x => x.Error).HasMaxLength(4000);

        builder.HasIndex(x => new { x.ContentId, x.CultureId }).IsUnique();
        builder.HasIndex(x => new { x.TranslationStatus, x.CultureId });

        builder.HasOne<Content>().WithMany().HasForeignKey(x => x.ContentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Culture>().WithMany().HasForeignKey(x => x.CultureId).OnDelete(DeleteBehavior.Restrict);
    }
}
