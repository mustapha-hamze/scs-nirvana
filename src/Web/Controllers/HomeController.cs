using Application.ContentManagement;
using Core.Services.TranslatorServices;

namespace Web.Controllers;

public class HomeController : Controller
{
    private readonly IContentProvider _contentProvider;
    private readonly IContentTranslator _contentTranslator;

    public HomeController(IContentProvider contentProvider, IContentTranslator contentTranslator)
    {
        _contentProvider = contentProvider;
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