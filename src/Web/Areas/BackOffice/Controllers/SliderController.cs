
using Web.Areas.BackOffice.Features.Slider.ViewModels;

namespace Web.Areas.BackOffice.Controllers;
[Authorize]
[Area("BackOffice")]
[Route("/BackOffice/{controller}/{action}")]
public class SliderController : BaseController
{
    // fields
    private readonly ISliderServices _sliderServices;
    private readonly ICurrentApplicationContext _currentApplicationContext;
    private readonly IHostEnvironment _appEnvironment;
    private readonly IFileUploadService _fileUploadService;
    private readonly AccessKeyAuthorizer _accessKeyAuthorizer;

    // constructor
    public SliderController(ISliderServices sliderServices,
        ICurrentApplicationContext currentApplicationContext, IHostEnvironment appEnvironment,
        IFileUploadService fileUploadService, AccessKeyAuthorizer accessKeyAuthorizer)
    {
        _sliderServices = sliderServices;
        _currentApplicationContext = currentApplicationContext;
        _appEnvironment = appEnvironment;
        _fileUploadService = fileUploadService;
        _accessKeyAuthorizer = accessKeyAuthorizer;
    }


    // methods
    [HttpGet]
    [RequireAccess(AccessKeys.Slider.Module)]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    [RequireAccess(AccessKeys.Slider.Module)]
    public async Task<IActionResult> List()
    {
        var currentApplicationId = _currentApplicationContext.RequireApplicationId();
        var sliders = await _sliderServices.GetSliders(currentApplicationId);
        var canAccessItems = await _accessKeyAuthorizer.HasAccessAsync(User, currentApplicationId, AccessKeys.Slider.AccessItems);

        var items = sliders.Select(s => new SliderListItemViewModel
        {
            Id = s.Id,
            Title = s.Title,
            CanAccessItems = canAccessItems,
        }).ToList();

        return View(new SliderListViewModel { Items = items });
    }
    // Views/Slider/_CreateSliderButton.cshtml gates navigation here with the (CMS-prefixed,
    // established as-is) AccessKeys.Slider.Add key - see AccessKeys.Slider.Add's own comment.
    [HttpGet]
    [RequireAccess(AccessKeys.Slider.Add)]
    public async Task<IActionResult> Create()
    {
        var currentApplicationId = _currentApplicationContext.RequireApplicationId();
        var canSave = await _accessKeyAuthorizer.HasAccessAsync(User, currentApplicationId, AccessKeys.Slider.Save);
        return View(new SliderCreateViewModel(currentApplicationId, canSave));
    }

    [HttpPost]
    [RequireAccess(AccessKeys.Slider.Save)]
    public async Task<IActionResult> Create(Slider slider)
    {
        var currentApplicationId = _currentApplicationContext.RequireApplicationId();
        slider.IsActive = true;
        await _sliderServices.Create(slider, currentApplicationId);
        return Ok();
    }

    [HttpGet]
    [RequireAccess(AccessKeys.Slider.AccessItems)]
    public IActionResult CreateItem(int sliderId)
    {
        ViewData["SliderId"] = sliderId;
        return View();
    }

    [HttpPost]
    [RequireAccess(AccessKeys.Slider.SaveItem)]
    public async Task<IActionResult> CreateItem(SliderItem sliderItem)
    {
        var currentApplicationId = _currentApplicationContext.RequireApplicationId();
        string imageName = Guid.NewGuid().ToString();
        sliderItem.ImageFileName = imageName + ".jpg";
        var _sliderItem = await _sliderServices.CreateSliderItem(sliderItem, currentApplicationId);

        return Ok($"{_sliderItem.SliderId}|{imageName}");
    }

    // Used by both the create-item and update-item forms (GetSliderItemForm.cshtml), so either
    // permission is accepted.
    [HttpPost]
    [RequireAccess(AccessKeys.Slider.SaveItem, AccessKeys.Slider.UpdateItem)]
    public async Task<IActionResult> UploadSliderItemImage(IFormFile file, int sliderId, string imageFileName)
    {
        var savePath = Path.Combine(_appEnvironment.ContentRootPath, "wwwroot/Storage/Slider/" + sliderId);
        var baseName = Path.GetFileNameWithoutExtension(imageFileName);

        // Slider items are created with a hardcoded ".jpg" file name (see CreateItem/UpdateItem) before the
        // image itself is uploaded, so the saved file must stay JPEG to match what was already persisted.
        var uploadResult = await _fileUploadService.SaveImageAsync(file, savePath, baseName, ImageOutputFormat.Jpeg);
        if (!uploadResult.Succeeded)
            return BadRequest(uploadResult.Error);

        return Ok(uploadResult.FileName);
    }

    [HttpGet]
    [RequireAccess(AccessKeys.Slider.AccessItems)]
    [Route("/{area}/{controller}/SliderItems")]
    public IActionResult SliderItems()
    {
        return View();
    }

    [HttpGet]
    [RequireAccess(AccessKeys.Slider.AccessItems)]
    [Route("/{area}/{controller}/GetSliderItemList/{sliderId}")]
    public async Task<IActionResult> GetSliderItemList(int sliderId)
    {
        var currentApplicationId = _currentApplicationContext.RequireApplicationId();
        var sliderItems = await _sliderServices.GetSliderItems(sliderId, currentApplicationId);
        var canToggleActivity = await _accessKeyAuthorizer.HasAccessAsync(User, currentApplicationId, AccessKeys.Slider.Activity);
        var canDelete = await _accessKeyAuthorizer.HasAccessAsync(User, currentApplicationId, AccessKeys.Slider.DeleteItem);
        var canUpdate = await _accessKeyAuthorizer.HasAccessAsync(User, currentApplicationId, AccessKeys.Slider.UpdateItem);

        var items = sliderItems.Select(i => new SliderItemRowViewModel
        {
            Id = i.Id,
            SliderId = i.SliderId,
            Title = i.Title,
            ImageFileName = i.ImageFileName,
            IsActive = i.IsActive,
        }).ToList();

        return View(new SliderItemListViewModel
        {
            Items = items,
            ImageVersion = Guid.NewGuid(),
            CanToggleActivity = canToggleActivity,
            CanDelete = canDelete,
            CanUpdate = canUpdate,
        });
    }

    [HttpGet]
    [RequireAccess(AccessKeys.Slider.AccessItems)]
    [Route("/{area}/{controller}/GetSliderItemForm/{sliderId}/{sliderItemId}")]
    public async Task<IActionResult> GetSliderItemForm(int sliderId, int sliderItemId = 0)
    {
        var currentApplicationId = _currentApplicationContext.RequireApplicationId();
        var sliderItem = sliderItemId != 0
            ? await _sliderServices.GetSliderItem(sliderItemId, currentApplicationId)
            : new SliderItem { SliderId = sliderId };

        var canCreateItem = await _accessKeyAuthorizer.HasAccessAsync(User, currentApplicationId, AccessKeys.Slider.SaveItem);
        var canUpdateItem = await _accessKeyAuthorizer.HasAccessAsync(User, currentApplicationId, AccessKeys.Slider.UpdateItem);

        var model = new SliderItemFormViewModel
        {
            Id = sliderItem.Id,
            SliderId = sliderItem.SliderId,
            Title = sliderItem.Title,
            Description = sliderItem.Description,
            Link = sliderItem.Link,
            ImageFileName = sliderItem.ImageFileName,
            Status = sliderItem.Status,
            IsDeleted = sliderItem.IsDeleted,
            IsActive = sliderItem.IsActive,
            UpdatedDT = sliderItem.UpdatedDT,
            CreatedDT = sliderItem.CreatedDT,
            ImageVersion = Guid.NewGuid(),
            CanCreateItem = canCreateItem,
            CanUpdateItem = canUpdateItem,
        };
        return View(model);
    }

    [HttpPost]
    [RequireAccess(AccessKeys.Slider.UpdateItem)]
    public async Task<IActionResult> UpdateItem(SliderItem model)
    {
        var currentApplicationId = _currentApplicationContext.RequireApplicationId();
        await _sliderServices.UpdateSliderItem(model, currentApplicationId);
        return Ok();
    }

    [HttpPost]
    [RequireAccess(AccessKeys.Slider.Activity)]
    public async Task<IActionResult> ActiveItem(int sliderItemId)
    {
        var currentApplicationId = _currentApplicationContext.RequireApplicationId();
        await _sliderServices.ActiveSliderItem(sliderItemId, currentApplicationId);
        return Ok();
    }

    [HttpPost]
    [RequireAccess(AccessKeys.Slider.Activity)]
    public async Task<IActionResult> DeactiveItem(int sliderItemId)
    {
        var currentApplicationId = _currentApplicationContext.RequireApplicationId();
        await _sliderServices.DeactiveSliderItem(sliderItemId, currentApplicationId);
        return Ok();
    }

    [HttpDelete]
    [RequireAccess(AccessKeys.Slider.DeleteItem)]
    public async Task<IActionResult> DeleteItem(int sliderItemId)
    {
        var currentApplicationId = _currentApplicationContext.RequireApplicationId();
        await _sliderServices.DeleteSliderItem(sliderItemId, currentApplicationId);
        return Ok();
    }
}