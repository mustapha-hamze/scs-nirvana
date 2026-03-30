using Application.ContentManagement;
using Core.Services.TranslatorServices;
using OpenAI.Chat;

namespace Web.Controllers;

public class HomeController : Controller
{
    private readonly IContentProvider _contentProvider;
    private readonly IContentTranslator _contentTranslator;
    private readonly ChatClient _client;
    private readonly string _apiKey = "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";
    public HomeController(IContentProvider contentProvider, IContentTranslator contentTranslator)
    {
        _contentProvider = contentProvider;
        _client = new ChatClient("gpt-5.2-chat-latest", _apiKey);
        _contentTranslator = contentTranslator;
    }
    public IActionResult Index()
    {
        return View();
    }
    public async Task<IActionResult> IndexTwo()
    {
        var content = await _contentProvider.GetContentForTranslate(3138);

        var result = await _contentTranslator.Translate(content);

        return Content(result);
    }
}