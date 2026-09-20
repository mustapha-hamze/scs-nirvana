using Domains.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

// Every BaseEntity-derived table shares the same key and audit-column shape. Centralizing it
// here means each IEntityTypeConfiguration only has to state what's actually distinct about
// that entity, and the PK/audit columns stay explicit (matching what convention already
// enforces today) without copy-pasting the same five lines into every configuration class.
public static class EntityTypeBuilderExtensions
{
    public static void ConfigureAudit<T>(this EntityTypeBuilder<T> builder) where T : BaseEntity
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.IsDeleted).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.UpdatedDT).IsRequired();
        builder.Property(x => x.CreatedDT).IsRequired();
    }
}
