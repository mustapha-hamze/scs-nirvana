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
