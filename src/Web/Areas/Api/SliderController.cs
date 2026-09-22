namespace Web.Areas.Api;
[ApiController]
[IgnoreAntiforgeryToken]
public class SliderController : ControllerBase
{
    private readonly ISliderServices _sliderServices;
    public SliderController(ISliderServices sliderServices)
    {
        _sliderServices = sliderServices;
    }
    // Breaking route change: applicationId is now a required leading segment so this public
    // endpoint can no longer be used to read another application's slider by guessing its id.
    [HttpGet]
    [Route("api/[controller]/{applicationId}/GetSlider/{sliderId}")]
    public IActionResult GetSlider(int applicationId, int sliderId)
    {
        return Ok(_sliderServices.GetSliderWithItems(sliderId, applicationId));
    }
}