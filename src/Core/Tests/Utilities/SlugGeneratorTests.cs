using Application.UseCases.Utilities.ApplicationFunctions;
using Xunit;

namespace Core.Tests.Utilities;

public class SlugGeneratorTests
{
    private readonly SlugGenerator _generator = new();

    [Fact]
    public void GenerateSlug_PersianTitle_PreservesLetters()
    {
        // Previously an ASCII-only whitelist dropped every Persian character, leaving this empty.
        var slug = _generator.GenerateSlug("سلام دنیا");

        Assert.Equal("سلام-دنیا", slug);
    }

    [Fact]
    public void GenerateSlug_MixedPersianAndLatin_KeepsBothScripts()
    {
        var slug = _generator.GenerateSlug("Nirvana سلام 2026");

        Assert.Equal("nirvana-سلام-2026", slug);
    }

    [Fact]
    public void GenerateSlug_RepeatedSeparators_CollapseDeterministically()
    {
        var slugA = _generator.GenerateSlug("Hello   ---   World");
        var slugB = _generator.GenerateSlug("Hello-World");

        Assert.Equal("hello-world", slugA);
        Assert.Equal(slugA, slugB);
    }

    [Fact]
    public void GenerateSlug_LeadingTrailingPunctuation_IsTrimmed()
    {
        var slug = _generator.GenerateSlug("!!! Title !!!");

        Assert.Equal("title", slug);
    }

    [Fact]
    public void GenerateSlug_EmptyInput_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, _generator.GenerateSlug(""));
    }
}
