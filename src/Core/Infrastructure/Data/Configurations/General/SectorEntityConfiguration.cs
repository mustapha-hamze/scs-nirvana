using Domains.Entities.AccessManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.General;

public class SectorEntityConfiguration : IEntityTypeConfiguration<SectorEntity>
{
    public void Configure(EntityTypeBuilder<SectorEntity> builder)
    {
        builder.ToTable("AME_SectorEntities");
        builder.ConfigureAudit();

        builder.Property(x => x.SectorId).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(64);
        builder.Property(x => x.AccessKey).HasMaxLength(256);

        builder.HasOne(x => x.Sector).WithMany(x => x.SectorEntities).HasForeignKey(x => x.SectorId);
    }
}
