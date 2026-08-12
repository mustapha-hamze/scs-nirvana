using Application.Slider;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Slider;

public class SliderProvider : ISliderProvider
{
    private readonly ApplicationDbContext _dbContext;
    public SliderProvider(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Domains.Entities.CustomModule.Slider> GetSlider(int sliderId)
    {
        var slider = await _dbContext.Sliders.Where(s => s.Id == sliderId)
            .Include(s => s.SliderItems.Where(si => si.IsActive && !si.IsDeleted)).ToListAsync();

        if (slider.Any())
            return slider[0];

        return new Domains.Entities.CustomModule.Slider();
    }
}