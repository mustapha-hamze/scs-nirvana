using Domains.Entities.ContentManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.CMS;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("CMS_Categories");
        builder.ConfigureAudit();

        builder.Property(x => x.ApplicationId).IsRequired();
        builder.Property(x => x.ParentId).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(256);
        builder.Property(x => x.Description).HasMaxLength(1024);

        builder.HasOne(x => x.Application).WithMany().HasForeignKey(x => x.ApplicationId);
    }
}
