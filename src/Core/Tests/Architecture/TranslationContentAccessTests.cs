using System.Linq;
using Application.ContentManagement;
using Xunit;

namespace Core.Tests.Architecture;

// Guards against reintroducing an unscoped translation-content lookup: every
// GetContentForTranslate overload on the public provider contract must require applicationId.
public class TranslationContentAccessTests
{
    [Fact]
    public void IContentProvider_HasNoBareContentIdOverload_ForGetContentForTranslate()
    {
        var overloads = typeof(IContentProvider).GetMethods()
            .Where(m => m.Name == nameof(IContentProvider.GetContentForTranslate))
            .ToList();

        Assert.NotEmpty(overloads);
        Assert.All(overloads, m => Assert.Contains(
            m.GetParameters(), p => p.Name == "applicationId"));
    }
}
