namespace Web.Areas.Api;
[ApiController]
public class SliderController : ControllerBase
{
    private readonly ISliderServices _sliderServices;
    public SliderController(ISliderServices sliderServices)
    {
        _sliderServices = sliderServices;
    }
    [Route("api/[controller]/GetSlider/{sliderId}")]
    public IActionResult GetSlider(int sliderId)
    {
        return Ok(_sliderServices.GetSliderWithItems(sliderId));
    }
}