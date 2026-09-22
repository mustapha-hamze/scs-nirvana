using Domains.Entities.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.General;

public class UserAttachmentConfiguration : IEntityTypeConfiguration<UserAttachment>
{
    public void Configure(EntityTypeBuilder<UserAttachment> builder)
    {
        builder.ToTable("GNR_UserAttachments");
        builder.ConfigureAudit();

        builder.Property(x => x.UserId).HasMaxLength(450);
        builder.Property(x => x.Title).HasMaxLength(256);
        builder.Property(x => x.AttachmentType).IsRequired();
    }
}
