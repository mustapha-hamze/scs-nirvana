using Application.UseCases.TranslatorServices;
using Xunit;

namespace Core.Tests.TranslatorServices;

public class TranslationOutputValidatorTests
{
    [Fact]
    public void Validate_ValidTranslation_ReturnsNull()
    {
        var original = "{\"Title\":\"Hello\",\"Id\":1,\"IsActive\":true}";
        var translated = "{\"Title\":\"سلام\",\"Id\":1,\"IsActive\":true}";

        var error = TranslationOutputValidator.Validate(original, translated);

        Assert.Null(error);
    }

    [Fact]
    public void Validate_InvalidJson_ReturnsError()
    {
        var original = "{\"Title\":\"Hello\"}";
        var translated = "not json at all";

        var error = TranslationOutputValidator.Validate(original, translated);

        Assert.Equal("Model response was not valid JSON.", error);
    }

    [Fact]
    public void Validate_ChangedProtectedField_ReturnsError()
    {
        // Id is not in the translatable field list - it must be byte-for-byte unchanged.
        var original = "{\"Title\":\"Hello\",\"Id\":1}";
        var translated = "{\"Title\":\"سلام\",\"Id\":2}";

        var error = TranslationOutputValidator.Validate(original, translated);

        Assert.NotNull(error);
        Assert.Contains("Id", error);
    }

    [Fact]
    public void Validate_ChangedStructureOrType_ReturnsError()
    {
        // Sections goes from an array to an object - a structural change, not a text edit.
        var original = "{\"Sections\":[{\"Priority\":1}]}";
        var translated = "{\"Sections\":{\"Priority\":1}}";

        var error = TranslationOutputValidator.Validate(original, translated);

        Assert.NotNull(error);
        Assert.Contains("Sections", error);
    }

    [Fact]
    public void Validate_TranslatableFieldTypeChanged_ReturnsError()
    {
        var original = "{\"Title\":\"Hello\"}";
        var translated = "{\"Title\":123}";

        var error = TranslationOutputValidator.Validate(original, translated);

        Assert.NotNull(error);
        Assert.Contains("Title", error);
    }

    [Fact]
    public void Validate_ExtraField_ReturnsError()
    {
        var original = "{\"Title\":\"Hello\"}";
        var translated = "{\"Title\":\"سلام\",\"Extra\":\"x\"}";

        var error = TranslationOutputValidator.Validate(original, translated);

        Assert.NotNull(error);
        Assert.Contains("Extra", error);
    }

    [Fact]
    public void Validate_MissingField_ReturnsError()
    {
        var original = "{\"Title\":\"Hello\",\"Id\":1}";
        var translated = "{\"Title\":\"سلام\"}";

        var error = TranslationOutputValidator.Validate(original, translated);

        Assert.NotNull(error);
        Assert.Contains("Id", error);
    }

    [Fact]
    public void Validate_RenamedField_ReturnsError()
    {
        var original = "{\"Title\":\"Hello\"}";
        var translated = "{\"Titel\":\"Hello\"}";

        var error = TranslationOutputValidator.Validate(original, translated);

        Assert.NotNull(error);
    }

    [Fact]
    public void Validate_ChangedHtmlMarkup_ReturnsError()
    {
        // strong became em - a markup edit, not a text-only translation.
        var original = "{\"EditorText\":\"<p>Hello <strong>world</strong></p>\"}";
        var translated = "{\"EditorText\":\"<p>سلام <em>دنیا</em></p>\"}";

        var error = TranslationOutputValidator.Validate(original, translated);

        Assert.NotNull(error);
        Assert.Contains("EditorText", error);
    }

    [Fact]
    public void Validate_SameHtmlMarkupWithTranslatedText_ReturnsNull()
    {
        var original = "{\"EditorText\":\"<p>Hello <strong>world</strong></p>\"}";
        var translated = "{\"EditorText\":\"<p>سلام <strong>دنیا</strong></p>\"}";

        var error = TranslationOutputValidator.Validate(original, translated);

        Assert.Null(error);
    }

    [Fact]
    public void Validate_PlainTextNoMarkup_ReturnsNull()
    {
        var original = "{\"EditorText\":\"Hello world\"}";
        var translated = "{\"EditorText\":\"سلام دنیا\"}";

        var error = TranslationOutputValidator.Validate(original, translated);

        Assert.Null(error);
    }

    [Fact]
    public void Validate_ChangedAttributeValue_ReturnsError()
    {
        var original = "{\"EditorText\":\"<a href=\\\"https://a.example\\\">Hello</a>\"}";
        var translated = "{\"EditorText\":\"<a href=\\\"https://b.example\\\">سلام</a>\"}";

        var error = TranslationOutputValidator.Validate(original, translated);

        Assert.NotNull(error);
        Assert.Contains("EditorText", error);
    }

    [Fact]
    public void Validate_ChangedNesting_ReturnsError()
    {
        // "B" moves from being a sibling of <p> to being reparented inside it.
        var original = "{\"EditorText\":\"<div><p>A</p><span>B</span></div>\"}";
        var translated = "{\"EditorText\":\"<div><p>A<span>B</span></p></div>\"}";

        var error = TranslationOutputValidator.Validate(original, translated);

        Assert.NotNull(error);
        Assert.Contains("EditorText", error);
    }

    [Fact]
    public void Validate_RemovedVoidElement_ReturnsError()
    {
        var original = "{\"EditorText\":\"<p>Hello<br></p>\"}";
        var translated = "{\"EditorText\":\"<p>سلام</p>\"}";

        var error = TranslationOutputValidator.Validate(original, translated);

        Assert.NotNull(error);
        Assert.Contains("EditorText", error);
    }

    [Fact]
    public void Validate_AddedVoidElement_ReturnsError()
    {
        var original = "{\"EditorText\":\"<p>Hello</p>\"}";
        var translated = "{\"EditorText\":\"<p>سلام<br></p>\"}";

        var error = TranslationOutputValidator.Validate(original, translated);

        Assert.NotNull(error);
        Assert.Contains("EditorText", error);
    }

    [Fact]
    public void Validate_GreaterThanInsideQuotedAttribute_UnchangedMarkup_ReturnsNull()
    {
        // The exact case a naive regex tag-matcher gets wrong: a '>' embedded in a quoted
        // attribute value must not be mistaken for the end of the tag.
        var original = "{\"EditorText\":\"<a title=\\\"a > b\\\">Hello</a>\"}";
        var translated = "{\"EditorText\":\"<a title=\\\"a > b\\\">سلام</a>\"}";

        var error = TranslationOutputValidator.Validate(original, translated);

        Assert.Null(error);
    }

    [Fact]
    public void Validate_GreaterThanInsideQuotedAttribute_ChangedValue_ReturnsError()
    {
        var original = "{\"EditorText\":\"<a title=\\\"a > b\\\">Hello</a>\"}";
        var translated = "{\"EditorText\":\"<a title=\\\"a > c\\\">سلام</a>\"}";

        var error = TranslationOutputValidator.Validate(original, translated);

        Assert.NotNull(error);
        Assert.Contains("EditorText", error);
    }

    [Fact]
    public void Validate_UnchangedComment_ReturnsNull()
    {
        var original = "{\"EditorText\":\"<p>Hello<!-- note --></p>\"}";
        var translated = "{\"EditorText\":\"<p>سلام<!-- note --></p>\"}";

        var error = TranslationOutputValidator.Validate(original, translated);

        Assert.Null(error);
    }

    [Fact]
    public void Validate_ChangedComment_ReturnsError()
    {
        var original = "{\"EditorText\":\"<p>Hello<!-- note --></p>\"}";
        var translated = "{\"EditorText\":\"<p>سلام<!-- changed --></p>\"}";

        var error = TranslationOutputValidator.Validate(original, translated);

        Assert.NotNull(error);
        Assert.Contains("EditorText", error);
    }

    [Fact]
    public void Validate_MalformedTranslatedHtml_ReturnsError()
    {
        // Unclosed <p>: the parser would recover/auto-close it, so this must be rejected on the
        // raw parse errors rather than after structural comparison of the recovered tree.
        var original = "{\"EditorText\":\"<p>Hello</p>\"}";
        var translated = "{\"EditorText\":\"<p>سلام\"}";

        var error = TranslationOutputValidator.Validate(original, translated);

        Assert.NotNull(error);
        Assert.Contains("EditorText", error);
    }

    [Fact]
    public void Validate_MalformedOriginalHtml_ReturnsError()
    {
        var original = "{\"EditorText\":\"<p>Hello\"}";
        var translated = "{\"EditorText\":\"<p>سلام</p>\"}";

        var error = TranslationOutputValidator.Validate(original, translated);

        Assert.NotNull(error);
        Assert.Contains("EditorText", error);
    }

    [Fact]
    public void Validate_ModifiedScriptText_ReturnsError()
    {
        var original = "{\"EditorText\":\"<p>Hello</p><script>alert('a');</script>\"}";
        var translated = "{\"EditorText\":\"<p>سلام</p><script>alert('b');</script>\"}";

        var error = TranslationOutputValidator.Validate(original, translated);

        Assert.NotNull(error);
        Assert.Contains("EditorText", error);
    }

    [Fact]
    public void Validate_ModifiedStyleText_ReturnsError()
    {
        var original = "{\"EditorText\":\"<p>Hello</p><style>.a{color:red;}</style>\"}";
        var translated = "{\"EditorText\":\"<p>سلام</p><style>.a{color:blue;}</style>\"}";

        var error = TranslationOutputValidator.Validate(original, translated);

        Assert.NotNull(error);
        Assert.Contains("EditorText", error);
    }

    [Fact]
    public void Validate_UnchangedScriptAndStyle_WithTranslatedSurroundingText_ReturnsNull()
    {
        var original = "{\"EditorText\":\"<p>Hello</p><script>alert('a');</script><style>.a{color:red;}</style><p>World</p>\"}";
        var translated = "{\"EditorText\":\"<p>سلام</p><script>alert('a');</script><style>.a{color:red;}</style><p>دنیا</p>\"}";

        var error = TranslationOutputValidator.Validate(original, translated);

        Assert.Null(error);
    }

    [Fact]
    public void Validate_NullFieldBecomesNonNull_ReturnsError()
    {
        var original = "{\"Title\":null}";
        var translated = "{\"Title\":\"سلام\"}";

        var error = TranslationOutputValidator.Validate(original, translated);

        Assert.NotNull(error);
    }

    [Fact]
    public void Validate_NonNullFieldBecomesNull_ReturnsError()
    {
        var original = "{\"Title\":\"Hello\"}";
        var translated = "{\"Title\":null}";

        var error = TranslationOutputValidator.Validate(original, translated);

        Assert.NotNull(error);
    }

    [Fact]
    public void Validate_NestedElementProtectedFieldChanged_ReturnsError()
    {
        var original = "{\"Sections\":[{\"Elements\":[{\"TinyText\":\"Hello\",\"ElementType\":1000}]}]}";
        var translated = "{\"Sections\":[{\"Elements\":[{\"TinyText\":\"سلام\",\"ElementType\":9999}]}]}";

        var error = TranslationOutputValidator.Validate(original, translated);

        Assert.NotNull(error);
        Assert.Contains("ElementType", error);
    }
}
