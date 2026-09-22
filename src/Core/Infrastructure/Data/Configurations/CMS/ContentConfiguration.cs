using Domains.Entities.ContentManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.CMS;

public class ContentConfiguration : IEntityTypeConfiguration<Content>
{
    public void Configure(EntityTypeBuilder<Content> builder)
    {
        builder.ToTable("CMS_Contents");
        builder.ConfigureAudit();

        builder.Property(x => x.ApplicationId).IsRequired();
        builder.Property(x => x.TypeId).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(256);
        builder.Property(x => x.HeadLine).HasMaxLength(2048);
        builder.Property(x => x.Abstract).HasMaxLength(2048);
        builder.Property(x => x.Categories).HasMaxLength(1024);
        builder.Property(x => x.Tags).HasMaxLength(1024);
        builder.Property(x => x.Cultures).HasMaxLength(1024);
        builder.Property(x => x.PublishDt).IsRequired();

        builder.HasOne(x => x.Application).WithMany(x => x.Contents).HasForeignKey(x => x.ApplicationId);
    }
}
