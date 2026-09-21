using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domains.Entities.CustomModule;

namespace Services.SCMServices
{
    public interface ISliderServices
    {
        Task<Slider> Create(Slider slider, int applicationId);
        Task<SliderItem> CreateSliderItem(SliderItem sliderItem, int applicationId);
        Task DeactiveSliderItem(int sliderItemId, int applicationId);
        List<Slider> GetSliders(int applicationId);
        List<SliderItem> GetSliderItems(int sliderId, int applicationId);
        Task DeleteSliderItem(int sliderItemId, int applicationId);
        Task ActiveSliderItem(int sliderItemId, int applicationId);
        Slider GetSliderWithItems(int sliderId, int applicationId);
        Task<SliderItem> GetSliderItem(int sliderItemId, int applicationId);
        Task<SliderItem> UpdateSliderItem(SliderItem sliderItem, int applicationId);
    }
}
