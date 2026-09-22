using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domains.Entities.CustomModule;

namespace Application.UseCases.SCMServices
{
    public interface ISliderServices
    {
        Task<Slider> Create(Slider slider, int applicationId, CancellationToken cancellationToken = default);
        Task<SliderItem> CreateSliderItem(SliderItem sliderItem, int applicationId, CancellationToken cancellationToken = default);
        Task DeactiveSliderItem(int sliderItemId, int applicationId, CancellationToken cancellationToken = default);
        Task<List<Slider>> GetSliders(int applicationId, CancellationToken cancellationToken = default);
        Task<List<SliderItem>> GetSliderItems(int sliderId, int applicationId, CancellationToken cancellationToken = default);
        Task DeleteSliderItem(int sliderItemId, int applicationId, CancellationToken cancellationToken = default);
        Task ActiveSliderItem(int sliderItemId, int applicationId, CancellationToken cancellationToken = default);
        Task<Slider> GetSliderWithItems(int sliderId, int applicationId, CancellationToken cancellationToken = default);
        Task<SliderItem> GetSliderItem(int sliderItemId, int applicationId, CancellationToken cancellationToken = default);
        Task<SliderItem> UpdateSliderItem(SliderItem sliderItem, int applicationId, CancellationToken cancellationToken = default);
    }
}