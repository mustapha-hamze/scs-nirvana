using Application.UseCases.Utilities.ApplicationFunctions;
using Xunit;

namespace Core.Tests.Utilities;

public class CodeGeneratorTests
{
    [Fact]
    public void GenerateAppKey_KeepsExpectedPrefixAndApplicationId()
    {
        var key = new CodeGenerator().GenerateAppKey(42);

        Assert.StartsWith("APP-KEY-NIRVANA-CMS-", key);
        Assert.Contains("-42-", key);
    }

    [Fact]
    public void GenerateAppKey_RandomComponent_DiffersAcrossCalls()
    {
        // Previously `new Random(123456789)` was re-seeded identically on every call, so this
        // component was the exact same value every time - not random at all.
        var generator = new CodeGenerator();

        var first = generator.GenerateAppKey(1).Split('-')[^1];
        var second = generator.GenerateAppKey(1).Split('-')[^1];

        Assert.NotEqual(first, second);
    }
}
