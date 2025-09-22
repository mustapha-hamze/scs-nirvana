namespace Application.Slider;

public interface ISliderProvider
{
    Task<Domains.Entities.CustomModule.Slider> GetSlider(int sliderId);
}
