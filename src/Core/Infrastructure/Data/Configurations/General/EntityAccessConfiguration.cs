using Domains.Entities.AccessManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.General;

public class EntityAccessConfiguration : IEntityTypeConfiguration<EntityAccess>
{
    public void Configure(EntityTypeBuilder<EntityAccess> builder)
    {
        builder.ToTable("AME_EntityAccesses");
        builder.ConfigureAudit();

        builder.Property(x => x.EntityId).IsRequired();
        builder.Property(x => x.Access).HasMaxLength(1024);

        builder.HasOne(x => x.SectorEntity).WithMany(x => x.EntityAccesses).HasForeignKey(x => x.EntityId);
    }
}
