using Application.AccessManagerRepository;
using Application.UseCases.AccessManagerServices;
using Xunit;

namespace Core.Tests.Architecture;

// The unscoped SectorEntity list read (GetAllEntities, no applicationId) let any tenant with
// access to the Access Management screen see every application's sector entities. Removed with
// no replacement in the tenant-facing contracts - GetEntitiesForApplication(applicationId) is the
// only way to list them now. This guards against either port silently growing an unscoped list
// method back in.
public class SectorEntityScopingGuardTests
{
    [Fact]
    public void SectorEntityRepositoryPort_HasNoUnscopedListMethod()
    {
        var methodNames = typeof(ISectorEntityRepository).GetMethods().Select(m => m.Name);

        Assert.DoesNotContain("GetAllEntities", methodNames);
        Assert.Contains("GetEntitiesForApplication", methodNames);
    }

    [Fact]
    public void SectorEntityServicesPort_HasNoUnscopedListMethod()
    {
        var methodNames = typeof(ISectorEntityServices).GetMethods().Select(m => m.Name);

        Assert.DoesNotContain("GetAllEntities", methodNames);
        Assert.Contains("GetEntitiesForApplication", methodNames);
    }
}
