using System.ComponentModel.DataAnnotations;

namespace Infrastructure.TranslatorServices;

public class OpenAiTranslationOptions
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "OPENAI_API_KEY is not configured.")]
    public string ApiKey { get; set; }

    public string Model { get; set; } = "gpt-5.6";

    // Full content graphs (all sections/HTML) can take well over the SDK's 100s default
    // before the model responds, so the default NetworkTimeout is too short.
    public int NetworkTimeoutMinutes { get; set; } = 5;
}
