

namespace Web.Areas.BackOffice.Controllers;
[Authorize]
[Area("BackOffice")]
[Route("/BackOffice/{controller}/{action}")]
public class SliderController : BaseController
{
    // fields
    private readonly ISliderServices _sliderServices;
    private readonly IUserManagementServices _userManagementServices;
    private readonly IHostEnvironment _appEnvironment;
    private readonly IFileUploadService _fileUploadService;

    // constructor
    public SliderController(ISliderServices sliderServices,
        IUserManagementServices userManagementServices, IHostEnvironment appEnvironment,
        IFileUploadService fileUploadService)
    {
        _sliderServices = sliderServices;
        _userManagementServices = userManagementServices;
        _appEnvironment = appEnvironment;
        _fileUploadService = fileUploadService;
    }


    // methods
    public IActionResult Index()
    {
        //TODO: Implement Realistic Implementation
        return View();
    }

    public IActionResult List()
    {
        //TODO: Implement Realistic Implementation
        var user = _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
        var slider = _sliderServices.GetSliders(user.CurrentApplicationId);
        return View(slider);
    }
    public IActionResult Create()
    {
        //TODO: Implement Realistic Implementation
        var user = _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
        ViewData["ApplicationId"] = user.CurrentApplicationId;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Slider slider)
    {
        //TODO: Implement Realistic Implementation
        slider.IsActive = true;
        await _sliderServices.Create(slider);
        return Ok();
    }

    public IActionResult CreateItem(int sliderId)
    {
        ViewData["SliderId"] = sliderId;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateItem(SliderItem sliderItem)
    {
        //TODO: Implement Realistic Implementation
        string imageName = Guid.NewGuid().ToString();
        sliderItem.ImageFileName = imageName + ".jpg";
        var _sliderItem = await _sliderServices.CreateSliderItem(sliderItem);

        return Ok($"{_sliderItem.SliderId}|{imageName}");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
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

    [Route("/{area}/{controller}/SliderItems")]
    public IActionResult SliderItems()
    {
        //TODO: Implement Realistic Implementation
        return View();
    }

    [Route("/{area}/{controller}/GetSliderItemList/{sliderId}")]
    public IActionResult GetSliderItemList(int sliderId)
    {
        var sliderItems = _sliderServices.GetSliderItems(sliderId);
        return View(sliderItems);
    }

    [Route("/{area}/{controller}/GetSliderItemForm/{sliderId}/{sliderItemId}")]
    public async Task<IActionResult> GetSliderItemForm(int sliderId, int sliderItemId = 0)
    {
        if (sliderItemId != 0)
        {
            return View(await _sliderServices.GetSliderItem(sliderItemId));
        }
        else
        {
            return View(new SliderItem { SliderId = sliderId });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateItem(SliderItem model)
    {
        //TODO: Implement Realistic Implementation
        await _sliderServices.UpdateSliderItem(model);
        return Ok();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ActiveItem(int sliderItemId)
    {
        //TODO: Implement Realistic Implementation
        await _sliderServices.ActiveSliderItem(sliderItemId);
        return Ok();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactiveItem(int sliderItemId)
    {
        //TODO: Implement Realistic Implementation
        await _sliderServices.DeactiveSliderItem(sliderItemId);
        return Ok();
    }

    [HttpDelete]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteItem(int sliderItemId)
    {
        //TODO: Implement Realistic Implementation
        await _sliderServices.DeleteSliderItem(sliderItemId);
        return Ok();
    }
}