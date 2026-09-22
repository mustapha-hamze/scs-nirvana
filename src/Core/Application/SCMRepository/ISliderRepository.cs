using System.Collections.Generic;
using System.Threading.Tasks;
using Domains.Entities.CustomModule;

namespace Application.SCMRepository
{
    public interface ISliderRepository
    {
        Task<Domains.Entities.CustomModule.Slider> Create(Domains.Entities.CustomModule.Slider slider);

        Task<List<Domains.Entities.CustomModule.Slider>> GetSliders(int applicationId, CancellationToken cancellationToken = default);
        Task<Domains.Entities.CustomModule.Slider> GetByIdForApplication(int id, int applicationId, CancellationToken cancellationToken = default);
        Task<List<SliderItem>> GetSliderItems(int sliderId, int applicationId, CancellationToken cancellationToken = default);
        Task<SliderItem> GetItemForApplication(int sliderItemId, int applicationId, CancellationToken cancellationToken = default);
        Task<SliderItem> CreateSliderItem(SliderItem sliderItem);
        Task DeactiveSliderItem(int sliderItemId, CancellationToken cancellationToken = default);
        Task DeleteSliderItem(int sliderItemId, CancellationToken cancellationToken = default);
        Task ActiveSliderItem(int sliderItemId, CancellationToken cancellationToken = default);
        Task<Domains.Entities.CustomModule.Slider> GetSliderWithItems(int sliderId, int applicationId, CancellationToken cancellationToken = default);
        Task<SliderItem> UpdateSliderItem(SliderItem sliderItem);
    }
}