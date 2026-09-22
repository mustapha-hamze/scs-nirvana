using Domains.Entities.ContentManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.CMS;

public class SchemaDetailsConfiguration : IEntityTypeConfiguration<SchemaDetails>
{
    public void Configure(EntityTypeBuilder<SchemaDetails> builder)
    {
        builder.ToTable("CMS_SchemaDetails");
        builder.ConfigureAudit();

        builder.Property(x => x.SchemaId).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(64);
        builder.Property(x => x.TypeId).IsRequired();
        builder.Property(x => x.Size).IsRequired();

        builder.HasOne(x => x.Schema).WithMany(x => x.Details).HasForeignKey(x => x.SchemaId);
    }
}
