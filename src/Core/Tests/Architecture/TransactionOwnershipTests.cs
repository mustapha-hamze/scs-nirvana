using System.Linq;
using System.Reflection;
using Xunit;

namespace Core.Tests.Architecture;

// Application (via IUnitOfWork) is the only transaction/save owner now - Infrastructure
// repositories may query and stage EF changes, but must never call SaveChanges or compose a
// transaction themselves. This scans compiled repository types rather than source, so it catches
// a violation regardless of which file (re)introduces the dependency.
public class TransactionOwnershipTests
{
    [Fact]
    public void InfrastructureRepositories_DoNotDependOnUnitOfWork()
    {
        var infrastructureAssembly = typeof(Infrastructure.Repository.Repository<>).Assembly;

        var repositoryTypes = infrastructureAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => t.Namespace != null && t.Namespace.Contains("Repository"));

        var violations = repositoryTypes
            .Where(t => t.GetConstructors()
                .SelectMany(c => c.GetParameters())
                .Any(p => p.ParameterType.FullName == "Application.UnitOfWork.IUnitOfWork"))
            .Select(t => t.FullName)
            .ToList();

        Assert.True(violations.Count == 0,
            $"Infrastructure repositories must not depend on IUnitOfWork: {string.Join(", ", violations)}");
    }
}
