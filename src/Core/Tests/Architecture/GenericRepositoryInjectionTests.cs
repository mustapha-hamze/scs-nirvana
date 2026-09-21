using System.Linq;
using Xunit;

namespace Core.Tests.Architecture;

// Application use-case services must depend on narrow, aggregate-specific repository ports
// (e.g. ISchemaRepository, IContentRepository) rather than the generic IRepository<T>, since a
// bare generic dependency has no way to express application-scoped ownership. This scans
// compiled service types rather than source, so it catches a violation regardless of which file
// (re)introduces the dependency.
public class GenericRepositoryInjectionTests
{
    [Fact]
    public void ApplicationServices_DoNotDependOnGenericRepository()
    {
        var applicationAssembly = typeof(Application.Repository.IRepository<>).Assembly;

        var serviceTypes = applicationAssembly.GetTypes()
            .Where(t => t.IsInterface || (t.IsClass && !t.IsAbstract))
            .Where(t => t.Namespace != null && t.Namespace.StartsWith("Services."));

        var violations = serviceTypes
            .Where(t => t.GetConstructors()
                .SelectMany(c => c.GetParameters())
                .Any(p => p.ParameterType.IsGenericType
                    && p.ParameterType.GetGenericTypeDefinition() == typeof(Application.Repository.IRepository<>)))
            .Select(t => t.FullName)
            .ToList();

        Assert.True(violations.Count == 0,
            $"Use-case services must not depend on generic IRepository<T>: {string.Join(", ", violations)}");
    }
}
