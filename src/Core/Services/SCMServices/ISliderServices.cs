using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domains.Entities.CustomModule;

namespace Services.SCMServices
{
    public interface ISliderServices
    {
        Task<Slider> Create(Slider slider);
        Task<SliderItem> CreateSliderItem(SliderItem sliderItem);
        Task DeactiveSliderItem(int sliderItemId);
        List<Slider> GetSliders(int applicationId);
        List<SliderItem> GetSliderItems(int sliderId);
        Task DeleteSliderItem(int sliderItemId);
        Task ActiveSliderItem(int sliderItemId);
        Slider GetSliderWithItems(int sliderId);
        Task<SliderItem> GetSliderItem(int sliderItemId);
        Task<SliderItem> UpdateSliderItem(SliderItem sliderItem);
    }
}