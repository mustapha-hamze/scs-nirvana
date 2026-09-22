using Domains.Entities.ContentManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.CMS;

public class ContentSectionConfiguration : IEntityTypeConfiguration<ContentSection>
{
    public void Configure(EntityTypeBuilder<ContentSection> builder)
    {
        builder.ToTable("CMS_ContentSections");
        builder.ConfigureAudit();

        builder.Property(x => x.ContentId).IsRequired();
        builder.Property(x => x.Priority).IsRequired();

        builder.HasOne(x => x.Content).WithMany(x => x.Sections).HasForeignKey(x => x.ContentId);
    }
}
