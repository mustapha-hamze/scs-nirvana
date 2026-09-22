using System.Linq;
using System.Reflection;
using Xunit;

namespace Core.Tests.Architecture;

// Domain and Application must stay independent of the concrete Infrastructure/Web layers so
// dependencies only ever point inward (Web/Infrastructure -> Application -> Domain). This
// inspects compiled assembly references rather than source, so it catches a violation
// regardless of which file introduces it.
public class LayerBoundaryTests
{
    private static readonly string[] ForbiddenLowerLayerReferences = { "Infrastructure", "Web" };

    [Theory]
    [InlineData(typeof(Domains.Entities.BaseEntity))]
    [InlineData(typeof(Application.Repository.IRepository<>))]
    public void Assembly_DoesNotReferenceInfrastructureOrWeb(System.Type typeFromAssembly)
    {
        var assembly = typeFromAssembly.Assembly;
        var referenced = assembly.GetReferencedAssemblies().Select(a => a.Name).ToList();

        var violations = referenced.Where(name => ForbiddenLowerLayerReferences.Contains(name)).ToList();

        Assert.True(violations.Count == 0,
            $"{assembly.GetName().Name} must not reference {string.Join(", ", violations)}.");
    }
}
