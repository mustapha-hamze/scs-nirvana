using Domains.Entities.ContentManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.CMS;

public class SchemaConfiguration : IEntityTypeConfiguration<Schema>
{
    public void Configure(EntityTypeBuilder<Schema> builder)
    {
        builder.ToTable("CMS_Schema");
        builder.ConfigureAudit();

        builder.Property(x => x.ApplicationId).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(64);
        builder.Property(x => x.LogoFileName).HasMaxLength(128);
        builder.Property(x => x.TypeId).IsRequired();

        // No [ForeignKey] on Schema.Application today and it isn't configured inline either;
        // left to EF's existing convention-based discovery rather than guessing a delete
        // behavior for it here.
    }
}
