using Domains.Entities.ContentManagement;
using Domains.Entities.General;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.CMS;

public class ContentInCultureConfiguration : IEntityTypeConfiguration<ContentInCulture>
{
    public void Configure(EntityTypeBuilder<ContentInCulture> builder)
    {
        builder.ToTable("CMS_ContentInCultures");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ContentId).IsRequired();
        builder.Property(x => x.CultureId).IsRequired();
        builder.HasIndex(x => new { x.ContentId, x.CultureId }).IsUnique();
        builder.HasOne<Content>().WithMany().HasForeignKey(x => x.ContentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Culture>().WithMany().HasForeignKey(x => x.CultureId).OnDelete(DeleteBehavior.Cascade);
    }
}
