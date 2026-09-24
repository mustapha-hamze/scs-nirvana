using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Web.Tests;

// Web Phase 5 task 4: two concise source guards that discover their own targets by scanning
// src/Web rather than checking a hand-maintained list of files/endpoints, so a future regression
// (an unbounded FormOptions limit) or a future upload endpoint that skips its own success check is
// caught the next time this suite runs, without anyone remembering to add it here. The third guard
// task 4 asks for - no live shared/sidebar endpoint reference lacking a route - already exists as
// BackOfficeSharedNavigationTests.SideBarLink_ResolvesToALiveRoute (added in the prior commit,
// itself already scan-driven); it is not duplicated here.
public sealed class WebUploadAndFormOptionsSourceGuardTests
{
    private static string WebProjectDir([CallerFilePath] string thisFilePath = "")
    {
        var testsDir = Path.GetDirectoryName(thisFilePath)!;
        var srcDir = Path.GetFullPath(Path.Combine(testsDir, ".."));
        return Path.Combine(srcDir, "Web");
    }

    private static IEnumerable<string> AllSourceFiles() =>
        Directory.GetFiles(WebProjectDir(), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    private static IEnumerable<string> ControllerFiles() =>
        AllSourceFiles().Where(f => f.Contains($"{Path.DirectorySeparatorChar}Controllers{Path.DirectorySeparatorChar}"));

    // ---- Guard 1: no unbounded FormOptions limit anywhere in src/Web ----

    [Fact]
    public void NoSourceFile_SetsAFormOptionsLimitToIntMaxValue()
    {
        var pattern = new Regex(@"(ValueCountLimit|ValueLengthLimit|MultipartBodyLengthLimit)\s*=\s*int\.MaxValue");

        var offending = AllSourceFiles()
            .Where(f => pattern.IsMatch(File.ReadAllText(f)))
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(offending.Count == 0, $"Found unbounded FormOptions limit(s) in: {string.Join(", ", offending)}");
    }

    // ---- Guard 2: every file-upload controller action checks success before returning ----

    [Fact]
    public void EveryFileUploadControllerAction_ChecksSuccessBeforeReturning()
    {
        var offending = new List<string>();

        foreach (var file in ControllerFiles())
        {
            var text = File.ReadAllText(file);
            foreach (var (name, signature, body) in ExtractMethods(text))
            {
                // Discovers a file-upload action by what it actually touches - an IFormFile/
                // IFormFileCollection parameter, or (UploadBodyImageGallery's shape) reading
                // Request.Form.Files directly with no formal file parameter - not by name.
                var touchesUpload = signature.Contains("IFormFile") || body.Contains("Request.Form.Files");
                if (!touchesUpload)
                    continue;

                var hasFailureBoundary = body.Contains(".Succeeded") || body.Contains("BadRequest(") || body.Contains("NotFound(");
                if (!hasFailureBoundary)
                    offending.Add($"{Path.GetFileName(file)}:{name}");
            }
        }

        Assert.True(offending.Count == 0,
            $"File-upload action(s) with no visible success/failure check: {string.Join(", ", offending)}");
    }

    // Naive but effective for this codebase's controller style: finds "<modifier> ... Name(...) {"
    // and returns everything up to the matching closing brace by counting braces from there. Good
    // enough to prove a body contains/doesn't contain a marker string - not a full C# parser.
    private static IEnumerable<(string Name, string Signature, string Body)> ExtractMethods(string source)
    {
        var signatureStart = new Regex(
            @"(?m)^[ \t]*(?:public|protected|internal|private)\s+(?:static\s+)?(?:async\s+)?[\w<>\[\],\.\?]+\s+(\w+)\s*\(");

        foreach (Match match in signatureStart.Matches(source))
        {
            var openParenIndex = match.Index + match.Length - 1;
            var closeParenIndex = FindMatching(source, openParenIndex, '(', ')');
            if (closeParenIndex < 0)
                continue;

            var braceStart = source.IndexOf('{', closeParenIndex);
            var semicolonIndex = source.IndexOf(';', closeParenIndex);
            if (braceStart < 0 || (semicolonIndex >= 0 && semicolonIndex < braceStart))
                continue; // an interface/abstract member ends in ';', not a body - skip it.

            var braceEnd = FindMatching(source, braceStart, '{', '}');
            if (braceEnd < 0)
                continue;

            var signature = source[match.Index..braceStart];
            var body = source[braceStart..(braceEnd + 1)];
            yield return (match.Groups[1].Value, signature, body);
        }
    }

    private static int FindMatching(string text, int openIndex, char open, char close)
    {
        var depth = 0;
        for (var i = openIndex; i < text.Length; i++)
        {
            if (text[i] == open) depth++;
            else if (text[i] == close)
            {
                depth--;
                if (depth == 0) return i;
            }
        }
        return -1;
    }
}
