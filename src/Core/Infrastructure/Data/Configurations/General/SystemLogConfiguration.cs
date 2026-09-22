using Domains.Entities.General;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.General;

public class SystemLogConfiguration : IEntityTypeConfiguration<SystemLog>
{
    public void Configure(EntityTypeBuilder<SystemLog> builder)
    {
        builder.ToTable("GRN_SystemLogs");
        builder.ConfigureAudit();

        builder.Property(x => x.ApplicationId).IsRequired();
        builder.Property(x => x.OperationCode).IsRequired();
        builder.Property(x => x.EntityId).IsRequired();
        builder.Property(x => x.OperationOwner).HasMaxLength(450);

        builder.HasOne(x => x.Application).WithMany(x => x.SystemLogs).HasForeignKey(x => x.ApplicationId);
    }
}
