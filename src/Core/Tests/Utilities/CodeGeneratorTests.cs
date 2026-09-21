using System;
using Application.UseCases.Utilities.ApplicationFunctions;
using Core.Tests.TestSupport;
using Xunit;

namespace Core.Tests.Utilities;

public class CodeGeneratorTests
{
    [Fact]
    public void GenerateAppKey_KeepsExpectedPrefixAndApplicationId()
    {
        var generator = new CodeGenerator(new FakeTimeProvider(DateTimeOffset.UtcNow));

        var key = generator.GenerateAppKey(42);

        Assert.StartsWith("APP-KEY-NIRVANA-CMS-", key);
        Assert.Contains("-42-", key);
    }

    [Fact]
    public void GenerateAppKey_FormatsCapturedInstantAsUtcyyyyMMddHHmmss()
    {
        var fixedInstant = new DateTimeOffset(2026, 3, 5, 13, 45, 9, TimeSpan.Zero);
        var generator = new CodeGenerator(new FakeTimeProvider(fixedInstant));

        var key = generator.GenerateAppKey(42);

        Assert.Equal("APP-KEY-NIRVANA-CMS-20260305134509-42-", key.Substring(0, key.LastIndexOf('-') + 1));
    }

    [Fact]
    public void GenerateAppKey_RandomComponent_DiffersAcrossCalls()
    {
        // Previously `new Random(123456789)` was re-seeded identically on every call, so this
        // component was the exact same value every time - not random at all.
        var generator = new CodeGenerator(new FakeTimeProvider(DateTimeOffset.UtcNow));

        var first = generator.GenerateAppKey(1).Split('-')[^1];
        var second = generator.GenerateAppKey(1).Split('-')[^1];

        Assert.NotEqual(first, second);
    }
}
