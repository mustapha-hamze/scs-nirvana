using System.Linq;
using Domains.Entities.ContentManagement;
using Domains.Entities.General;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace Core.Tests.Architecture;

// Guards the CMS_ContentTranslations model against drift from the DBA SQL contract.
public class ContentTranslationModelTests
{
    // Built against the SQL Server provider (never connects) so store types reflect production.
    private static IEntityType EntityType()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=unused")
            .Options;
        using var context = new ApplicationDbContext(options, System.TimeProvider.System);
        return context.Model.FindEntityType(typeof(ContentTranslation));
    }

    [Fact]
    public void MapsToTableWithAuditAndSoftDeleteFilter()
    {
        var entity = EntityType();

        Assert.Equal("CMS_ContentTranslations", entity.GetTableName());
        Assert.NotNull(entity.GetDeclaredQueryFilters().SingleOrDefault());
        Assert.False(entity.FindProperty(nameof(ContentTranslation.Status)).IsNullable);
    }

    [Theory]
    [InlineData(nameof(ContentTranslation.ContentId), false, "int", null)]
    [InlineData(nameof(ContentTranslation.CultureId), false, "int", null)]
    [InlineData(nameof(ContentTranslation.TranslationStatus), false, "tinyint", null)]
    [InlineData(nameof(ContentTranslation.SourceFingerprint), false, "varchar(64)", 64)]
    [InlineData(nameof(ContentTranslation.LocalizedTextJson), true, "nvarchar(max)", null)]
    [InlineData(nameof(ContentTranslation.Provider), true, "varchar(128)", 128)]
    [InlineData(nameof(ContentTranslation.Model), true, "varchar(128)", 128)]
    [InlineData(nameof(ContentTranslation.TranslatedAt), true, "datetime2", null)]
    [InlineData(nameof(ContentTranslation.Error), true, "nvarchar(4000)", 4000)]
    public void Column_MatchesContract(string name, bool nullable, string columnType, int? maxLength)
    {
        var property = EntityType().FindProperty(name);

        Assert.Equal(nullable, property.IsNullable);
        Assert.Equal(columnType, property.GetRelationalTypeMapping().StoreType);
        Assert.Equal(maxLength, property.GetMaxLength());
    }

    [Fact]
    public void Indexes_MatchContract()
    {
        var indexes = EntityType().GetIndexes()
            .ToDictionary(i => string.Join(",", i.Properties.Select(p => p.Name)), i => i.IsUnique);

        Assert.True(indexes["ContentId,CultureId"]);
        Assert.False(indexes["TranslationStatus,CultureId"]);
    }

    [Theory]
    [InlineData(typeof(Content), nameof(ContentTranslation.ContentId))]
    [InlineData(typeof(Culture), nameof(ContentTranslation.CultureId))]
    public void ForeignKey_DoesNotCascade(System.Type principal, string foreignKeyProperty)
    {
        var fk = EntityType().GetForeignKeys()
            .Single(f => f.PrincipalEntityType.ClrType == principal);

        Assert.Equal(foreignKeyProperty, fk.Properties.Single().Name);
        Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
    }
}
