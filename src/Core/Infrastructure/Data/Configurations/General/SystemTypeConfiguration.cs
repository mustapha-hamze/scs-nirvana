using Domains.Entities.General;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.General;

public class SystemTypeConfiguration : IEntityTypeConfiguration<SystemType>
{
    public void Configure(EntityTypeBuilder<SystemType> builder)
    {
        builder.ToTable("GNR_SystemTypes");
        builder.ConfigureAudit();

        builder.Property(x => x.ApplicationId).IsRequired();
        builder.Property(x => x.TypeGroupId).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(128);
        builder.Property(x => x.IsRTL).IsRequired();

        builder.HasOne(x => x.Application).WithMany(x => x.SystemTypes).HasForeignKey(x => x.ApplicationId);
    }
}
