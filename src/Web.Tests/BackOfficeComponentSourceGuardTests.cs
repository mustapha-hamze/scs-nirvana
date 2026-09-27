using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Web.Tests;

// Source-level guard for the shared BackOffice component rules (docs/backoffice-design-system.md):
// in-page commands are native buttons, every modal is labelled with a named close control, and the
// shared helpers load from js/features/common.js (behaviour tested in js/common.test.mjs).
public sealed class BackOfficeComponentSourceGuardTests
{
    private static string ViewsPath([CallerFilePath] string thisFilePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFilePath)!, "..", "Web", "Areas", "BackOffice", "Views"));

    private static string[] ViewFiles() => Directory.GetFiles(ViewsPath(), "*.cshtml", SearchOption.AllDirectories);

    private static string[] Offending(Func<string, bool> isBad) =>
        ViewFiles().Where(f => isBad(File.ReadAllText(f))).Select(f => Path.GetRelativePath(ViewsPath(), f)).ToArray();

    [Fact]
    public void Commands_AreButtons_NotJavascriptLinks()
    {
        Assert.Empty(Offending(text => Regex.IsMatch(text, "href=\"javascript:", RegexOptions.IgnoreCase)));
    }

    [Fact]
    public void ModalCloseButtons_HaveAnAccessibleName()
    {
        Assert.Empty(Offending(text => Regex.Matches(text, "<button[^>]*class=\"btn-close\"[^>]*>")
            .Any(m => !m.Value.Contains("aria-label=") || m.Value.Contains("aria-hidden"))));
    }

    [Fact]
    public void Modals_AreLabelledByTheirOwnTitle()
    {
        Assert.Empty(Offending(text => Regex.Matches(text, "class=\"modal fade\"[^>]*aria-labelledby=\"([^\"]+)\"|class=\"modal fade\"(?![^>]*aria-labelledby)[^>]*>")
            .Any(m => !m.Groups[1].Success || !text.Contains($"class=\"modal-title\" id=\"{m.Groups[1].Value}\""))));
    }

    [Theory]
    [InlineData("Shared/_Layout.cshtml")]
    [InlineData("Shared/_JSAssets.cshtml")]
    public void SharedHelpers_LoadFromCommonScript_NotInlineCopies(string file)
    {
        var text = File.ReadAllText(Path.Combine(ViewsPath(), file));
        Assert.Contains("src=\"/BackOffice/js/features/common.js\"", text);
        Assert.DoesNotContain("function setLoadingForBtn", text);
        Assert.DoesNotContain("function checkFormValidity", text);
    }
}
