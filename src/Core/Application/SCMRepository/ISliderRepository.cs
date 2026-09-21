using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domains.Entities.CustomModule;
using Application.Repository;

namespace Application.SCMRepository
{
    public interface ISliderRepository : IRepository<Domains.Entities.CustomModule.Slider>
    {
        List<Domains.Entities.CustomModule.Slider> GetSliders(int applicationId);
        Task<Domains.Entities.CustomModule.Slider> GetByIdForApplication(int id, int applicationId);
        List<SliderItem> GetSliderItems(int sliderId, int applicationId);
        Task<SliderItem> GetItemForApplication(int sliderItemId, int applicationId);
        Task<SliderItem> CreateSliderItem(SliderItem sliderItem);
        Task DeactiveSliderItem(int sliderItemId);
        Task DeleteSliderItem(int sliderItemId);
        Task ActiveSliderItem(int sliderItemId);
        Domains.Entities.CustomModule.Slider GetSliderWithItems(int sliderId, int applicationId);
        Task<SliderItem> UpdateSliderItem(SliderItem sliderItem);
    }
}
