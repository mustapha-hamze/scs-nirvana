using Application.ContentManagement;
using Application.UseCases.TranslatorServices;
using Microsoft.AspNetCore.Http;

namespace Web.Areas.BackOffice.Controllers;

// Split across Areas/BackOffice/Controllers/Content/ into focused partials: this file (shared
// dependencies/composition), .Forms (content forms/lifecycle), .Farsi (Farsi localization),
// .Relations (relations/metadata), .Sections (sections/layout), .Uploads (uploads/images). One
// public ContentController type, one [Area]/route/constructor - the split is file-only.
[Authorize]
[Area("BackOffice")]
[Route("/BackOffice/{controller}/{action}")]
public partial class ContentController : BaseController
{
    private readonly IContentServices _contentServices;
    private readonly IContentProvider _contentProvider;
    private readonly ISchemaServices _schemaServices;
    private readonly ICategoryServices _categoryServices;
    private readonly ITagServices _tagServices;
    private readonly ICultureServices _cultureServices;
    private readonly IHostEnvironment _appEnvironment;
    private readonly IApplicationServices _applicationServices;
    private readonly ISystemTypeServices _systemTypeServices;
    private readonly ICurrentApplicationContext _currentApplicationContext;
    private readonly IContentTranslator _contentTranslator;
    private readonly IFileUploadService _fileUploadService;
    private readonly AccessKeyAuthorizer _accessKeyAuthorizer;

    public ContentController(IContentServices contentServices, ISchemaServices schemaServices,
        ICategoryServices categoryServices,
        ITagServices tagServices, ICultureServices cultureServices, IHostEnvironment appEnvironment,
        IApplicationServices applicationServices, ISystemTypeServices systemTypeServices,
        ICurrentApplicationContext currentApplicationContext, IContentTranslator contentTranslator,
        IContentProvider contentProvider, IFileUploadService fileUploadService,
        AccessKeyAuthorizer accessKeyAuthorizer)
    {
        _applicationServices = applicationServices;
        _contentServices = contentServices;
        _schemaServices = schemaServices;
        _categoryServices = categoryServices;
        _tagServices = tagServices;
        _cultureServices = cultureServices;
        _appEnvironment = appEnvironment;
        _systemTypeServices = systemTypeServices;
        _currentApplicationContext = currentApplicationContext;
        _contentTranslator = contentTranslator;
        _contentProvider = contentProvider;
        _fileUploadService = fileUploadService;
        _accessKeyAuthorizer = accessKeyAuthorizer;
    }

    // ContentForm/SaveContentForm serve both create and edit through one action (see .Forms.cs),
    // so the required key depends on request data (id/content.Id) - RequireAccessAttribute can
    // only express a static, per-action key, so those two call this directly instead. Still runs
    // after RequireTenantContextFilter, since it executes from within the action body.
    private async Task<IActionResult> DenyIfMissingAccessAsync(string key)
    {
        var hasAccess = await _accessKeyAuthorizer.HasAccessAsync(User, _currentApplicationContext.RequireApplicationId(), key);
        return hasAccess ? null : StatusCode(StatusCodes.Status403Forbidden);
    }
}
