using Domains.Entities.General;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.General;

public class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("GNR_Tags");
        builder.ConfigureAudit();

        builder.Property(x => x.ApplicationId).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(64);
        builder.Property(x => x.TypeId).IsRequired();

        builder.HasOne(x => x.Application).WithMany(x => x.Tags).HasForeignKey(x => x.ApplicationId);
    }
}
