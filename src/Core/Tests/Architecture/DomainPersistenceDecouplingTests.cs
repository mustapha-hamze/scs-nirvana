using System.Linq;
using Core.Tests.TestSupport;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Core.Tests.Architecture;

// Domain entities are plain data carriers - all persistence shape (max length, required-ness,
// foreign keys, table names) lives in Infrastructure/Data/Configurations. This proves Domain
// stays free of that concern and that the Fluent configurations actually carry forward the exact
// constraints the old [StringLength]/[ForeignKey] attributes used to declare, so removing those
// attributes from Domain didn't silently change schema semantics.
public class DomainPersistenceDecouplingTests
{
    [Fact]
    public void Domain_HasNoDataAnnotationsOrEfCoreAssemblyReference()
    {
        var referenced = typeof(Domains.Entities.BaseEntity).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name);

        var violations = referenced
            .Where(name => name.Contains("DataAnnotations") || name.StartsWith("Microsoft.EntityFrameworkCore"))
            .ToList();

        Assert.True(violations.Count == 0,
            $"Domain must not reference DataAnnotations or EF Core assemblies, found: {string.Join(", ", violations)}.");
    }

    [Theory]
    [InlineData(typeof(Domains.Entities.ContentManagement.Content), nameof(Domains.Entities.ContentManagement.Content.Title), 256)]
    [InlineData(typeof(Domains.Entities.ContentManagement.Content), nameof(Domains.Entities.ContentManagement.Content.HeadLine), 2048)]
    [InlineData(typeof(Domains.Entities.General.Application), nameof(Domains.Entities.General.Application.ApplicationKey), 128)]
    [InlineData(typeof(Domains.Entities.General.UserAccess), nameof(Domains.Entities.General.UserAccess.Access), 4096)]
    [InlineData(typeof(Domains.Entities.ContentManagement.SectionElement), nameof(Domains.Entities.ContentManagement.SectionElement.GalleryImages), 4092)]
    public void Configuration_PreservesFormerAnnotationMaxLength(System.Type entityType, string propertyName, int expectedMaxLength)
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();

        var entityModel = context.Model.FindEntityType(entityType);
        var property = entityModel.FindProperty(propertyName);

        Assert.Equal(expectedMaxLength, property.GetMaxLength());
    }

    [Theory]
    [InlineData(typeof(Domains.Entities.ContentManagement.Content), nameof(Domains.Entities.ContentManagement.Content.ApplicationId))]
    [InlineData(typeof(Domains.Entities.ContentManagement.ContentMetadata), nameof(Domains.Entities.ContentManagement.ContentMetadata.ContentId))]
    [InlineData(typeof(Domains.Entities.ContentManagement.SchemaDetails), nameof(Domains.Entities.ContentManagement.SchemaDetails.SchemaId))]
    [InlineData(typeof(Domains.Entities.CustomModule.SliderItem), nameof(Domains.Entities.CustomModule.SliderItem.SliderId))]
    [InlineData(typeof(Domains.Entities.AccessManagement.EntityAccess), nameof(Domains.Entities.AccessManagement.EntityAccess.EntityId))]
    public void Configuration_PreservesFormerForeignKey(System.Type entityType, string foreignKeyPropertyName)
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();

        var entityModel = context.Model.FindEntityType(entityType);
        var hasForeignKey = entityModel.GetForeignKeys()
            .Any(fk => fk.Properties.Any(p => p.Name == foreignKeyPropertyName));

        Assert.True(hasForeignKey, $"{entityType.Name}.{foreignKeyPropertyName} lost its foreign key relationship.");
    }
}
