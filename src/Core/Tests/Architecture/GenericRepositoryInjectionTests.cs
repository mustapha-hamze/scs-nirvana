using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Core.Tests.Architecture;

// Tenant-owned aggregate ports must not inherit the generic IRepository<T> - a bare generic
// interface exposes unscoped GetById/Delete with no way to require the caller's applicationId,
// which is exactly the escape hatch a cross-tenant id could walk through. UserAttachment is the
// one deliberate exception (owner-scoped by userId, not tenant-scoped by applicationId - see
// IUserAttachmentRepository), so it's excluded here rather than silently reclassified.
public class GenericRepositoryInjectionTests
{
    private static readonly Type[] TenantOwnedPorts =
    {
        typeof(Application.CMSRepository.IContentCommandRepository),
        typeof(Application.CMSRepository.ISchemaRepository),
        typeof(Application.CMSRepository.ICategoryRepository),
        typeof(Application.GeneralRepository.ITagRepository),
        typeof(Application.GeneralRepository.ICultureRepository),
        typeof(Application.GeneralRepository.IApplicationRepository),
        typeof(Application.GeneralRepository.ISystemTypeRepository),
        typeof(Application.SCMRepository.ISliderRepository),
        typeof(Application.AccessManagerRepository.ISectorRepository),
        typeof(Application.AccessManagerRepository.ISectorEntityRepository),
        typeof(Application.AccessManagerRepository.IEntityAccessRepository),
    };

    private static bool InheritsGenericRepository(Type type) =>
        type.GetInterfaces().Concat(new[] { type })
            .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(Application.Repository.IRepository<>));

    [Fact]
    public void TenantOwnedPorts_DoNotInheritGenericIRepository()
    {
        var violations = TenantOwnedPorts.Where(InheritsGenericRepository).Select(t => t.FullName).ToList();

        Assert.True(violations.Count == 0,
            $"Tenant-owned repository ports must not inherit IRepository<T>: {string.Join(", ", violations)}");
    }

    [Fact]
    public void ApplicationServices_DoNotDependOnGenericRepositoryForATenantPort()
    {
        // A use-case service could still type a constructor parameter as the generic
        // IRepository<T> directly (bypassing a specific port entirely) for one of the tenant
        // entities above - this catches that regardless of which port interface exists today.
        var tenantEntityTypes = new[]
        {
            typeof(Domains.Entities.ContentManagement.Content),
            typeof(Domains.Entities.ContentManagement.Schema),
            typeof(Domains.Entities.ContentManagement.Category),
            typeof(Domains.Entities.General.Tag),
            typeof(Domains.Entities.General.Culture),
            typeof(Domains.Entities.General.Application),
            typeof(Domains.Entities.General.SystemType),
            typeof(Domains.Entities.CustomModule.Slider),
            typeof(Domains.Entities.AccessManagement.Sector),
            typeof(Domains.Entities.AccessManagement.SectorEntity),
            typeof(Domains.Entities.AccessManagement.EntityAccess),
        };

        var applicationAssembly = typeof(Application.Repository.IRepository<>).Assembly;
        var serviceTypes = applicationAssembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract);

        var violations = serviceTypes
            .Where(t => t.GetConstructors()
                .SelectMany(c => c.GetParameters())
                .Any(p => p.ParameterType.IsGenericType
                    && p.ParameterType.GetGenericTypeDefinition() == typeof(Application.Repository.IRepository<>)
                    && tenantEntityTypes.Contains(p.ParameterType.GetGenericArguments()[0])))
            .Select(t => t.FullName)
            .ToList();

        Assert.True(violations.Count == 0,
            $"Use-case services must not depend on generic IRepository<T> for a tenant-owned entity: {string.Join(", ", violations)}");
    }

    [Fact]
    public void SpecializedTenantRepositories_DoNotConstructGenericRepositoryForTenantChildren()
    {
        // Infrastructure repositories that own a tenant child entity (ApplicationSetting under
        // Application, ContentSection/SectionElement/ContentMetadata/ContentImage under Content,
        // SchemaDetails under Schema) must operate on it through the owning DbContext directly,
        // not by wrapping it in another generic Repository<T> instance.
        var specializedRepositoryTypes = new[]
        {
            typeof(Infrastructure.GeneralRepository.ApplicationRepository),
            typeof(Infrastructure.CMSRepository.ContentCommandRepository),
            typeof(Infrastructure.CMSRepository.SchemaRepository),
        };

        var genericRepositoryType = typeof(Infrastructure.Repository.Repository<>);

        var violations = specializedRepositoryTypes
            .SelectMany(t => t.GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(f => f.FieldType.IsGenericType && f.FieldType.GetGenericTypeDefinition() == genericRepositoryType)
                .Select(f => $"{t.Name}.{f.Name}"))
            .ToList();

        Assert.True(violations.Count == 0,
            $"Found generic Repository<T> field(s) for a tenant child entity: {string.Join(", ", violations)}");
    }
}
